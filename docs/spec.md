---
title: Manifest Spec
description: SponsorLink Manifest Specification
nav_order: 2
has_children: true
has_toc: false
current: 2.0.1
---

{%- assign versions = site[page.collection]
  | default: site.html_pages
  | where: "parent", page.title -%}

{% for spec in versions -%}
{% if spec.title == page.current -%}
[{{ spec.title }}]({{ spec.url | relative_url }}){: .btn .btn-blue }
{% else -%}
[{{ spec.title }}]({{ spec.url | relative_url }}){: .btn }
{% endif -%}
{% endfor -%}

{% capture source %}{% include_relative spec/{{ page.current }}.md %}{% endcapture %}
<!-- Remove front-matter from included markdown. We rely on our fragment spec -->
{{ source | split: "<!-- #content -->" | last }}
