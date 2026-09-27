#!/usr/bin/env python3
"""Draws the diagrams of the README into docs/images. Run from the repository root:

    python3 scripts/diagrams/storefront.py
"""
import os, sys
sys.path.insert(0, os.path.dirname(__file__))
from draw import write_both

OUT = "docs/images"
os.makedirs(OUT, exist_ok=True)


def backend(d, x, y, w, h, kicker, name, ports, stores):
    d.card(x, y, w, h, "green", name, [ports], kicker=kicker, title_size=16)
    sx = x + 14
    for icon, label in stores:
        d.icon(icon, sx, y + h - 60, 18)
        d.text(sx + 24, y + h - 46, label, size=10.8, fill=d.t["muted"])
        sx += 34 + len(label) * 5.8
    d.chip(x + 12, y + h - 31, "built on MP Core", "blue", size=10, pad=8)


def system(d):
    d.heading(32, 44, "Storefront: three backends that work together",
              "Everything in this picture runs on your machine with four commands, and on GitHub on every change.")
    L, LW, R, RW = 32, 676, 728, 240

    callers = ["Shoppers", "Back office", "Warehouse system", "Analysts"]
    for i, name in enumerate(callers):
        x = L + i * 171
        d.rect(x, 84, 163, 42, d.fill("slate"), d.stroke("slate"), r=21, shadow=True)
        d.icon("people", x + 12, 94, 22)
        d.text(x + 42, 110, name, size=12.3, weight=600, fill=d.ink("slate"))
    d.arrow([(L + LW / 2, 128), (L + LW / 2, 156)], sw=1.8)
    d.text(L + LW / 2 + 10, 147, "HTTPS  ·  gRPC over TLS", size=10.5, fill=d.t["muted"])

    d.group(L, 168, LW, 84, "rose", "EDGE")
    d.tool(L + 16, 188, LW - 32, 52, "apisix", "Apache APISIX",
           ["One door for the three: ends TLS, routes REST and gRPC, names every request, limits anonymous browsing"], "run")
    d.arrow([(L + LW / 2, 254), (L + LW / 2, 282)], sw=1.8)

    # ---- the three backends
    y, h = 294, 236
    d.card(L, y, 356, h, "green", "Commerce", ["REST :5100   ·   gRPC :5101"], kicker="modular monolith", title_size=16)
    modules = [("Catalog", "products, prices, stock"), ("Basket", "the basket, checkout"),
               ("Ordering", "the order and its process"), ("Payments", "intents, charges, refunds")]
    for i, (name, what) in enumerate(modules):
        mx, my = L + 14 + (i % 2) * 168, y + 86 + (i // 2) * 50
        d.rect(mx, my, 160, 42, d.t["canvas"], d.stroke("green"), r=8, sw=1.1)
        d.text(mx + 10, my + 18, name, size=12, weight=700, fill=d.ink("green"))
        d.text(mx + 10, my + 33, what, size=10.3, fill=d.t["muted"])
    d.icon("postgresql", L + 14, y + h - 40, 18)
    d.text(L + 38, y + h - 26, "one schema each", size=10.8, fill=d.t["muted"])
    d.icon("redis", L + 138, y + h - 40, 18)
    d.text(L + 162, y + h - 26, "cache", size=10.8, fill=d.t["muted"])
    d.chip(L + 232, y + h - 43, "built on MP Core", "blue", size=10, pad=8)

    for i, (name, ports, lines, icon, store) in enumerate([
        ("Fulfillment", "gRPC :5201", ["The warehouse:", "what is to be shipped,", "what has left"], "postgresql", "its own database"),
        ("Analytics", "REST :5300", ["The figures:", "sales per hour", "and per city"], "timescale", "a hypertable"),
    ]):
        x = L + 368 + i * 158
        d.card(x, y, 150, h, "green", name, [ports, ""] + lines, kicker="service", title_size=16)
        d.icon(icon, x + 14, y + h - 66, 18)
        d.text(x + 38, y + h - 52, store, size=10.8, fill=d.t["muted"])
        d.chip(x + 12, y + h - 35, "built on MP Core", "blue", size=10, pad=8)

    # ---- messaging between them
    d.arrow([(L + 178, y + h + 2), (L + 178, y + h + 30)], sw=1.8, both=True)
    d.arrow([(L + 443, y + h + 2), (L + 443, y + h + 30)], sw=1.8, both=True)
    d.arrow([(L + 601, y + h + 30), (L + 601, y + h + 2)], sw=1.8)
    gy = y + h + 42
    d.group(L, gy, LW, 106, "amber", "MESSAGES  ·  NO SHARED CODE BETWEEN THE THREE")
    d.tool(L + 16, gy + 22, 316, 70, "rabbitmq", "RabbitMQ", ["Commerce  →  order ready to ship  →  Fulfillment", "Commerce  ←  shipment dispatched  ←  Fulfillment"], "run", size=24)
    d.tool(L + 344, gy + 22, 316, 70, "apachekafka", "Apache Kafka", ["Commerce  →  order placed, paid, cancelled", "→  Analytics, from the start of the stream"], "run", size=24)

    # ---- the right column
    d.group(R, 168, RW, 84, "purple", "IDENTITY")
    d.tool(R + 12, 188, RW - 24, 52, "keycloak", "Keycloak", ["One audience per backend"], "run")
    d.arrow([(L + LW + 2, 330), (R + 40, 330), (R + 40, 256)], sw=1.5, dash="5 4")
    d.text(R + 50, 324, "every backend validates", size=10.5, fill=d.t["muted"])
    d.text(R + 50, 338, "every token, itself", size=10.5, fill=d.t["muted"])

    d.group(R, 372, RW, 84, "teal", "PAYMENT PROVIDER")
    d.tool(R + 12, 392, RW - 24, 52, "wiremock", "DemoPay", ["Simulated with WireMock"], "run")
    d.arrow([(L + 356 + 2, 418), (R - 2, 418)], sw=1.5, dash="5 4") if False else None

    d.group(R, 488, RW, 190, "slate", "OBSERVABILITY")
    d.tool(R + 12, 510, RW - 24, 48, "opentelemetry", "OpenTelemetry", [], "run", size=24)
    for k, (ic, name) in enumerate([("jaeger", "Jaeger"), ("prometheus", "Prometheus"), ("grafana", "Grafana")]):
        d.tile(R + 12 + k * 74, 568, 68, 84, ic, name, None, "run", size=24)
    d.text(R + 12, 668, "One trace crosses the three backends", size=10.5, fill=d.t["muted"])

    d.status(L + 8, gy + 136, "run")
    d.text(L + 20, gy + 140, "Run end to end by the 21 scenarios of scripts/scenarios.sh", size=11.5, fill=d.t["muted"])


def journey(d):
    d.heading(32, 44, "One order, from a basket to a parcel",
              "Every step is one transaction in one module. The line in blue is what MP Core guarantees there.")
    steps = [
        ("teal", "Payments", "A payment intent", ["The card token goes to Payments", "and nowhere else"], "No message carries a secret"),
        ("green", "Basket", "Checkout", ["The basket is emptied and", "announced. The answer is 202"], "One commit, with the message"),
        ("purple", "Ordering", "The order is created", ["From the message, under the", "identity the basket gave it"], "Inbox: handled once"),
        ("teal", "Payments", "The charge is registered", ["One payment per order;", "the intent is used once"], "A transaction of its own"),
        ("amber", "Catalog", "The stock is reserved", ["All lines, or none. Twelve", "orders may want one product"], "Retry, with random pauses"),
        ("teal", "Payments", "The card is charged", ["The provider may be slow,", "or down, or say no"], "Resilient HTTP client"),
        ("purple", "Ordering", "The order is paid", ["Told to the figures (Kafka) and", "to the warehouse (RabbitMQ)"], "Outbox: sent if committed"),
        ("rose", "Fulfillment", "The parcel leaves", ["Another service, over gRPC;", "the shop hears and ships"], "Two services, no shared code"),
    ]
    w, h, gx, gy, x0, y0 = 224, 168, 10, 22, 32, 88
    for i, (color, module, title, lines, promise) in enumerate(steps):
        col, row = i % 4, i // 4
        x, y = x0 + col * (w + gx), y0 + row * (h + gy)
        d.card(x, y, w, h, color, title, lines, kicker=module)
        d.badge(x + w - 20, y + 26, str(i + 1), color)
        d.line(x + 14, y + h - 40, x + w - 14, y + h - 40, color=d.stroke(color), sw=1, dash="3 4")
        d.icon("mpcore", x + 14, y + h - 30, 18)
        d.text(x + 38, y + h - 17, promise, size=10.6, weight=600, fill=d.ink("blue"))
        if col:
            d.arrow([(x - gx + 1, y + h / 2), (x - 1, y + h / 2)], sw=1.6)
    # from the end of the first row to the start of the second
    d.arrow([(x0 + 4 * w + 3 * gx + 8, y0 + h / 2), (x0 + 4 * w + 3 * gx + 18, y0 + h / 2), (x0 + 4 * w + 3 * gx + 18, y0 + h + gy / 2),
             (x0 - 12, y0 + h + gy / 2), (x0 - 12, y0 + h + gy + h / 2), (x0 - 2, y0 + h + gy + h / 2)], sw=1.6)


write_both(f"{OUT}/system", 1000, 730, "The Storefront system: a gateway, three backends, two brokers, identity and observability", system)
write_both(f"{OUT}/order-journey", 1000, 480, "The eight steps of an order across the modules and services, and what MP Core guarantees at each", journey)
print("drawn:", ", ".join(sorted(os.listdir(OUT))))
