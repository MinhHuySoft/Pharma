"""Snapshot public catalog pages and import them into a local SQL Server database."""

import argparse
from concurrent.futures import ThreadPoolExecutor
from datetime import datetime, timezone
from decimal import Decimal, InvalidOperation
from hashlib import sha256
from io import BytesIO
import json
from pathlib import Path
import re
import subprocess
import tempfile
from threading import local
from time import sleep
from urllib.parse import urljoin, urlsplit
from urllib.robotparser import RobotFileParser
from xml.etree import ElementTree

from bs4 import BeautifulSoup
import nh3
from PIL import Image, ImageOps
import requests
from requests.adapters import HTTPAdapter
from urllib3.util.retry import Retry

ROOT = Path(__file__).resolve().parents[1]
BASE = "https://tribinhpharma.com/"
DATA = ROOT / "Database"
SNAPSHOT = DATA / "catalog.json"
MEDIA = ROOT / "wwwroot" / "source-media"
AGENT = "PharmaLocalCatalog/1.0"
XML_NS = {"s": "http://www.sitemaps.org/schemas/sitemap/0.9"}
ORIGIN = urlsplit(BASE).netloc
CONTENT_TAGS = set("a abbr b blockquote br caption cite code dd del details div dl dt em figcaption figure h2 h3 h4 h5 h6 hr i img li ol p pre s small span strong sub summary sup table tbody td tfoot th thead tr u ul".split())
CONTENT_ATTRIBUTES = {
    "*": {"id"}, "a": {"href", "title"}, "img": {"src", "alt", "width", "height", "loading", "decoding"},
    "td": {"colspan", "rowspan"}, "th": {"colspan", "rowspan", "scope"},
    "details": {"open", "class"}, "ol": {"start"}
}


def text(tag):
    return " ".join(tag.stripped_strings) if tag else ""


def heading_text(tag):
    return " ".join(text(tag).split())


def normalize_heading_markup(heading):
    for block in heading.find_all("div"):
        block.name = "span"
        block.insert_before(" ")
        block.insert_after(" ")
    for node in list(heading.find_all(string=True)):
        normalized = re.sub(r"\s+", " ", str(node))
        if normalized != node:
            node.replace_with(normalized)


def slug(url):
    return urlsplit(url).path.rstrip("/").split("/")[-1]


class Crawler:
    def __init__(self):
        self.sessions = local()
        self.robots = RobotFileParser()
        self.robots.parse(self.fetch(BASE + "robots.txt", check_robots=False).text.splitlines())
        self.paths = {"/", "/cua-hang", "/gioi-thieu", "/lien-he", "/gio-hang", "/tai-khoan", "/thanh-toan"}
        self.missing_images = set()

    def fetch(self, url, check_robots=True):
        if not hasattr(self.sessions, "http"):
            self.sessions.http = requests.Session()
            self.sessions.http.headers["User-Agent"] = AGENT
            retries = Retry(total=2, backoff_factor=1, status_forcelist=[429, 502, 503, 504])
            self.sessions.http.mount(BASE, HTTPAdapter(max_retries=retries))
        for _ in range(4):
            parts = urlsplit(url)
            if parts.scheme != "https" or parts.netloc != ORIGIN or parts.query:
                raise ValueError(f"Outside crawl scope: {url}")
            if check_robots and not self.robots.can_fetch(AGENT, url):
                raise ValueError(f"Excluded by robots.txt: {url}")
            sleep(0.25)
            response = self.sessions.http.get(url, timeout=25, allow_redirects=False)
            if response.is_redirect:
                url = urljoin(url, response.headers["Location"])
                continue
            response.raise_for_status()
            if response.status_code == 200:
                return response
            raise ValueError(f"Expected 200, got {response.status_code}: {url}")
        raise ValueError(f"Too many redirects: {url}")

    def sitemap(self, name):
        sitemap = ElementTree.fromstring(self.fetch(BASE + name).content)
        indexes = sitemap.findall("s:sitemap/s:loc", XML_NS)
        if indexes:
            return [url for index in indexes for url in self.sitemap(urlsplit(index.text).path.lstrip("/"))]
        return [loc.text for loc in sitemap.findall("s:url/s:loc", XML_NS)]

    def page(self, url):
        return BeautifulSoup(self.fetch(url).content, "html.parser", from_encoding="utf-8")

    def asset(self, url):
        url = urljoin(BASE, url).split("?")[0]
        suffix = Path(urlsplit(url).path).suffix.lower()
        if suffix not in {".png", ".jpg", ".jpeg", ".webp", ".gif", ".avif"}:
            raise ValueError(f"Unsupported image: {url}")
        identifier = sha256(url.encode()).hexdigest()[:20]
        filename = identifier + ".webp"
        file = MEDIA / filename
        if not file.exists():
            response = self.fetch(url)
            if not response.headers.get("Content-Type", "").startswith("image/"):
                raise ValueError(f"Expected image: {url}")
            save_webp(BytesIO(response.content), file)
        return f"/source-media/{filename}", url

    def image(self, tag):
        return self.asset(tag.get("data-src") or tag["src"])

    def local_link(self, href, url):
        if href.startswith("#"):
            return href
        resolved = urlsplit(urljoin(url, href))
        path = resolved.path.rstrip("/") or "/"
        if resolved.hostname == ORIGIN and not resolved.query:
            if path == "/danh-muc-san-pham-cua-tri-binh-pharm":
                path += "a"
            if path in self.paths:
                return path + ("#" + resolved.fragment if resolved.fragment else "")
        return resolved.geturl()

    def optional_image(self, url):
        if not url:
            return ""
        url = urljoin(BASE, url)
        if urlsplit(url).hostname != ORIGIN or url in self.missing_images:
            return ""
        try:
            return self.asset(url)[0]
        except (requests.RequestException, ValueError, OSError):
            self.missing_images.add(url)
            return ""

    def content(self, root, url, title):
        if root is None:
            return ""
        for node in root.select("#ez-toc-container, #toc_container, script, style, form, noscript, .sharedaddy, .wp-block-button__width-100 .wp-block-button__link"):
            node.decompose()
        document = BeautifulSoup(str(root), "html.parser")
        sections = []
        for heading in list(document.select("h1, h2, h3, h4, h5, h6")):
            normalize_heading_markup(heading)
            label = heading_text(heading)
            if not label or label.casefold() == title.casefold():
                heading.decompose()
                continue
            if heading.name == "h1":
                heading.name = "h2"
            if heading.name == "h2":
                anchor = heading.get("id") or f"muc-{len(sections) + 1}"
                heading["id"] = anchor
                sections.append((anchor, label))
        for link in document.select("a[href]"):
            link["href"] = self.local_link(link["href"], url)
        for image in document.select("img"):
            path = self.optional_image(image.get("data-src") or image.get("src"))
            if path:
                image["src"] = path
                image["loading"] = "lazy"
                image["decoding"] = "async"
            else:
                image.decompose()
        for iframe in document.select("iframe[src]"):
            link = document.new_tag("a", href=iframe["src"])
            label = iframe.get("title", "").strip()
            link.string = label or "Mở nội dung nhúng"
            iframe.replace_with(link)
        if len(sections) >= 3:
            toc = document.new_tag("details", attrs={"class": "content-toc"})
            label = document.new_tag("summary", string="Nội dung trên trang")
            links = document.new_tag("ol")
            for anchor, title in sections:
                item = document.new_tag("li")
                item.append(document.new_tag("a", href=f"#{anchor}", string=title))
                links.append(item)
            toc.append(label)
            toc.append(links)
            document.insert(0, toc)
        return nh3.clean(str(document), tags=CONTENT_TAGS, attributes=CONTENT_ATTRIBUTES,
                         url_schemes={"https", "http", "mailto", "tel"},
                         clean_content_tags={"script", "style", "svg", "math", "form", "iframe", "object"},
                         link_rel="noopener noreferrer")

    def article(self, url):
        page = self.page(url)
        article = page.select_one("main article")
        title = heading_text(article.select_one("h1.entry-title, h1") if article else None)
        root = article.select_one(".entry-content") if article else None
        if not title or not root:
            raise ValueError(f"Article content missing: {url}")
        sections = [name.removeprefix("category-") for name in article.get("class", []) if name.startswith("category-")]
        stamp = page.select_one('meta[property="article:published_time"]')
        summary = page.select_one('meta[property="og:description"]')
        image = page.select_one('meta[property="og:image"]')
        author = article.select_one(".author a, a[rel=author]")
        body = self.content(root, url, title)
        print(f"Article: {slug(url)}", flush=True)
        return dict(Slug=slug(url), Title=title, Summary=summary["content"] if summary else text(root.find("p"))[:240],
                    Section=next(iter(sections), "blog-tu-van"), ImagePath=self.optional_image(image["content"] if image else None),
                    PublishedOn=stamp["content"][:10] if stamp else None, SourceUrl=url,
                    AuthorName=text(author) or "Trí Bình Pharma", BodyHtml=body)

    def site_page(self, url):
        page = self.page(url)
        main = page.select_one("main")
        title = heading_text(main.select_one("h1, h2"))
        root = main.select_one(".entry-content")
        if not title or not root:
            raise ValueError(f"Page content missing: {url}")
        if slug(url) == "lien-he":
            contact_info = BeautifulSoup(str(root), "html.parser")
            for node in contact_info.select(".wpcf7, .elementor-widget-google_maps"):
                node.decompose()
            root = contact_info
        return dict(Slug=slug(url), Title=title, BodyHtml=self.content(root, url, title), SourceUrl=url)

    def product(self, url, order, thumbnail, card_classes):
        page = self.page(url)
        title = page.select_one("h1.product_title")
        if not title:
            raise ValueError(f"Product title missing: {url}")
        category = page.select_one(".product_meta .posted_in a[href]")
        if not category:
            raise ValueError(f"Category missing: {url}")
        facts = {text(row.select_one(".txt")).rstrip(": "): text(row.select_one("a"))
                 for row in page.select(".product_meta > span:has(> .txt):has(> a)")}
        schema = next((node for tag in page.select('script[type="application/ld+json"]')
                       if isinstance(node := json.loads(tag.get_text()), dict) and node.get("@type") == "Product"), {})
        offers = schema.get("offers", [])
        offer = (offers[0] if offers else {}) if isinstance(offers, list) else offers
        amounts = [offer, *offer.get("priceSpecification", [])]
        raw_price = next((str(a["price"]) for a in amounts if a.get("priceCurrency") == "VND" and a.get("price")), "")
        try:
            amount = Decimal(raw_price)
            price = int(amount) if 0 < amount <= 2147483647 and amount == amount.to_integral_value() else None
        except InvalidOperation:
            price = None
        status = page.select_one(".product_meta .status, .summary .stock")
        in_stock = offer.get("availability", "").endswith("/InStock")
        if status:
            in_stock = text(status).casefold() == "còn hàng"
        product = dict(Slug=slug(url), CategorySlug=slug(category["href"]), Name=heading_text(title),
                       Summary=text(page.select_one(".woocommerce-product-details__short-description")),
                       PriceVnd=price, InStock=in_stock, Packaging=facts.get("Quy cách đóng gói"),
                       Origin=facts.get("Xuất xứ"), Manufacturer=facts.get("Nhà sản xuất"),
                       Distributor=facts.get("Nhà phân phối"), SourceUrl=url, SortOrder=order,
                       BodyHtml=self.content(page.select_one("#tab-description .woocommerce-Tabs-panel__content, #tab-description"), url, heading_text(title)))
        gallery = [a["href"] for a in page.select(".woocommerce-product-gallery__image a[href]")]
        if not gallery:
            image = page.select_one('meta[property="og:image"]')
            if image:
                gallery = [image["content"]]
        urls = dict.fromkeys(([thumbnail] if thumbnail else []) + gallery)
        images = []
        for index, image_url in enumerate(urls):
            path, source = self.asset(image_url)
            images.append(dict(ProductSlug=product["Slug"], SortOrder=index, Path=path, SourceUrl=source))
        if not images:
            raise ValueError(f"No images found: {url}")
        groups = [(slug(a["href"]), text(a), urlsplit(a["href"]).path.rstrip("/"))
                  for a in page.select(".product_meta .tagged_as a[href]")]
        product_root = page.select_one('main [id^="product-"]')
        for name in set(card_classes) | set(product_root.get("class", []) if product_root else []):
            if name.startswith("pa_danh-muc-"):
                key = name.removeprefix("pa_danh-muc-")
                groups.append((key, self.attribute_names.get(key, key.replace("-", " ").capitalize()), f"/danh-muc/{key}"))
        print(f"Product: {product['Slug']}", flush=True)
        return product, images, groups

    def snapshot(self):
        MEDIA.mkdir(parents=True, exist_ok=True)
        urls = [url for url in self.sitemap("product-sitemap.xml") if slug(url) != "cua-hang"]
        article_urls = self.sitemap("post-sitemap.xml")
        group_urls = self.sitemap("pa_danh-muc-sitemap.xml")
        archive_urls = self.sitemap("category-sitemap.xml") + self.sitemap("author-sitemap.xml")
        category_urls = self.sitemap("product_cat-sitemap.xml")
        self.paths.update(urlsplit(u).path.rstrip("/") for u in urls + article_urls + group_urls + archive_urls + category_urls)
        self.attribute_names = dict(zip(
            ("cham-soc-lan-da", "cham-soc-mat", "cham-soc-phu-khoa", "cham-soc-thai-ki", "ho-tro-sinh-san",
             "kem-tri-seo", "phu-nu-tien-man-kinh-man-kinh"),
            ("Chăm sóc làn da", "Chăm sóc mắt", "Chăm sóc phụ khoa", "Chăm sóc thai kì", "Hỗ trợ sinh sản",
             "Kem trị sẹo", "Phụ nữ tiền mãn kinh, mãn kinh"), strict=True))
        home = self.page(BASE)
        categories = []
        for index, url in enumerate(category_urls):
            name = text(self.page(url).select_one("h1.woocommerce-products-header__title, main h1, .page-title"))
            if not name:
                raise ValueError(f"Category title missing: {url}")
            categories.append(dict(Slug=slug(url), Name=name, SourceUrl=url, SortOrder=index))

        cards = home.select(".elementor-115 a.product-loop__image")
        thumbnails = {a["href"]: a.img.get("data-src") or a.img["src"] for a in cards if a.img}
        card_classes = {a["href"]: a.find_parent("li").get("class", []) for a in cards}
        homepage_order = {url: index for index, url in enumerate(thumbnails)}
        urls.sort(key=lambda url: homepage_order.get(url, len(homepage_order)))
        products, images, groups, memberships = [], [], {}, []
        with ThreadPoolExecutor(max_workers=4) as pool:
            product_jobs = pool.map(lambda task: self.product(task[1], task[0], thumbnails.get(task[1]),
                                                              card_classes.get(task[1], [])), enumerate(urls))
            article_jobs = pool.map(self.article, article_urls)
            page_jobs = pool.map(self.site_page, [BASE + "gioi-thieu/", BASE + "lien-he/"])
            for product, gallery, product_groups in product_jobs:
                products.append(product)
                images.extend(gallery)
                product["BodyHtml"] = product["BodyHtml"] or ""
                for key, name, path in dict.fromkeys(product_groups):
                    groups[path] = dict(Path=path, Name=name, SourceUrl=BASE.rstrip("/") + path + "/")
                    memberships.append(dict(GroupPath=path, ProductSlug=product["Slug"]))
            articles = list(article_jobs)
            pages = list(page_jobs)
        articles.sort(key=lambda a: (a["PublishedOn"] or "", a["Slug"]), reverse=True)
        for index, article in enumerate(articles):
            article["SortOrder"] = index

        assets = []
        def add(key, tag):
            path, source = self.image(tag)
            link = tag.find_parent("a", href=True)
            assets.append(dict(AssetKey=key, Path=path, SourceUrl=source, AltText=tag.get("alt", ""),
                               LinkUrl=self.local_link(link["href"], BASE) if link and link["href"].startswith(BASE) else None))
        for key, selector in {
            "logo": 'header img[alt="Trí Bình Pharma"]',
            "hero": ".elementor-element-2b51480 img",
            "banner-products": ".elementor-element-e1db520 img",
            "banner-news": ".elementor-element-a6e971f img",
        }.items():
            tag = home.select_one(selector)
            if not tag:
                raise ValueError(f"Homepage image missing: {key}")
            add(key, tag)
        for prefix, selector in (("partner", ".elementor-element-9459a80 img"),
                                 ("feature", ".elementor-element-90a40ab img")):
            for index, tag in enumerate(home.select(selector), 1):
                add(f"{prefix}-{index:02}", tag)
        return dict(capturedAt=datetime.now(timezone.utc).isoformat(), categories=categories,
                    products=products, images=images, articles=articles, assets=assets, pages=pages,
                    productGroups=list(groups.values()), productGroupItems=memberships,
                    missingImages=sorted(self.missing_images))


def save_webp(source, destination):
    with Image.open(source) as image:
        image = ImageOps.exif_transpose(image)
        image.thumbnail((1440, 1440), Image.Resampling.LANCZOS)
        if image.mode not in {"RGB", "RGBA"}:
            image = image.convert("RGBA" if "transparency" in image.info else "RGB")
        with tempfile.NamedTemporaryFile(dir=destination.parent, suffix=".tmp", delete=False) as temp:
            temp_file = Path(temp.name)
            try:
                image.save(temp, format="WEBP", quality=88, method=4)
            except Exception:
                temp.close()
                temp_file.unlink(missing_ok=True)
                raise
        temp_file.replace(destination)


def sqlcmd(server, database, file):
    subprocess.run(["sqlcmd", "-S", server, "-E", "-C", "-b", "-x", "-f", "65001",
                    "-d", database, "-i", str(file)], check=True)


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--server", default="lpc:.", help="Local SQL Server instance (default: lpc:.)")
    parser.add_argument("--crawl", action="store_true", help="Refresh the catalog snapshot and images from the site")
    parser.add_argument("--crawl-only", action="store_true", help="Capture the website without writing to SQL Server")
    args = parser.parse_args()
    if args.crawl or args.crawl_only:
        snapshot = Crawler().snapshot()
        temporary = SNAPSHOT.with_suffix(".tmp")
        temporary.write_text(json.dumps(snapshot, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
        temporary.replace(SNAPSHOT)
    if args.crawl_only:
        return

    snapshot = json.loads(SNAPSHOT.read_text(encoding="utf-8"))
    json_sql = json.dumps(snapshot, ensure_ascii=False, separators=(",", ":"))
    # Keep individual lines short enough for sqlcmd, including when the full article corpus exceeds 1 MB.
    chunks = (json_sql[index:index + 3000] for index in range(0, len(json_sql), 3000))
    literals = "\n".join(f"SET @snapshot += N'{chunk.replace(chr(39), chr(39) * 2)}';" for chunk in chunks)
    sql = "DECLARE @snapshot nvarchar(max) = N'';\n" + literals + "\n" + (DATA / "02-import.sql").read_text(encoding="utf-8")
    sqlcmd(args.server, "master", DATA / "01-schema.sql")
    with tempfile.TemporaryDirectory(prefix="pharma-import-") as directory:
        batch = Path(directory) / "catalog.sql"
        batch.write_text(sql, encoding="utf-8")
        sqlcmd(args.server, "Pharma", batch)


if __name__ == "__main__":
    main()
