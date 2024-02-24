---
title: Manifest Spec 
nav_order: 4
has_children: true
has_toc: false
current: 1.0.0-rc
---

{%- assign versions = site[page.collection]
  | default: site.html_pages
  | where: "parent", page.title -%}

[Current](spec/{{ page.current }}.html){: .btn .btn-blue }
{% for spec in versions -%}
[{{ spec.title }}]({{ spec.url | relative_url }}){: .btn }
{% endfor -%}

{% capture source %}{% include_relative spec/{{ page.current }}.md %}{% endcapture %}
<!-- Remove front-matter from included markdown. We rely on our fragment spec -->
{{ source | split: "<!-- #content -->" | last }}
