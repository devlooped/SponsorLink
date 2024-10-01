---
title: Manifest Spec
description: SponsorLink Manifest Specification
nav_order: 2
has_children: true
has_toc: false
---

{%- assign versions = site[page.collection]
  | default: site.html_pages
  | where: "parent", page.title -%}  

{% for spec in versions -%}
    {%- assign delimited = ';' | append: spec.title | append: ';' -%}
    {%- unless site.spec_skip contains delimited -%}
      {% if spec.title == site.spec -%}
[{{ spec.title }}]({{ spec.url | relative_url }}){: .btn .btn-blue }
      {% else -%}
[{{ spec.title }}]({{ spec.url | relative_url }}){: .btn }
      {% endif -%}
    {%- endunless -%}
{% endfor -%}

{% capture source %}{% include_relative spec/{{ site.spec }}.md %}{% endcapture %}
<!-- Remove front-matter from included markdown. We rely on our fragment spec -->
{{ source | split: "<!-- #content -->" | last }}
