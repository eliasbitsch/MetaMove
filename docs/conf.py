# Configuration file for the Sphinx documentation builder.
# MetaMove Technical Documentation
#
# Build HTML:  sphinx-build -b html  . _build/html
# Build PDF :  sphinx-build -b rinoh . _build/pdf     (pure-Python, no LaTeX)

project = "MetaMove"
author = "Elias Bitsch, Viktoriia Ovdiienko, Philip Stix"
copyright = "2026, Elias Bitsch, Viktoriia Ovdiienko, Philip Stix"
release = "1.0"
version = "1.0"

extensions = [
    "myst_parser",
]

myst_enable_extensions = [
    "colon_fence",
    "deflist",
    "fieldlist",
    "tasklist",
    "linkify",
]
myst_heading_anchors = 3

source_suffix = {
    ".md": "markdown",
}

root_doc = "index"
master_doc = "index"

exclude_patterns = ["_build", "Thumbs.db", ".DS_Store", "README.md", "ui-mockups", "markers"]

# --- HTML output -----------------------------------------------------------
html_theme = "furo"
html_title = "MetaMove Technical Documentation"
html_show_sourcelink = False

# --- PDF output (rinohtype, sphinx-build -b rinoh) -------------------------
# Short title + subtitle so the running page header stays on one line.
# The template config (metamove.rtt) pulls in metamove.rts (no hyphenation).
rinoh_documents = [
    dict(
        doc="index",
        target="MetaMove-Technical-Documentation",
        title="MetaMove",
        subtitle="Technical Documentation",
        author="Elias Bitsch · Viktoriia Ovdiienko · Philip Stix",
        template="metamove.rtt",
    )
]
