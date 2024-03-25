# Copyright (c) Microsoft. All rights reserved.
from __future__ import annotations

from typing import Literal, get_args

KERNEL_TEMPLATE_FORMAT_NAME_TYPE = Literal["semantic-kernel"]
KERNEL_TEMPLATE_FORMAT_NAME = get_args(KERNEL_TEMPLATE_FORMAT_NAME_TYPE)[0]
HANDLEBARS_TEMPLATE_FORMAT_NAME_TYPE = Literal["handlebars"]
HANDLEBARS_TEMPLATE_FORMAT_NAME = get_args(HANDLEBARS_TEMPLATE_FORMAT_NAME_TYPE)[0]
JINJA2_TEMPLATE_FORMAT_NAME_TYPE = Literal["jinja2"]
JINJA2_TEMPLATE_FORMAT_NAME = get_args(JINJA2_TEMPLATE_FORMAT_NAME_TYPE)[0]

TEMPLATE_FORMAT_TYPES = Literal[
    KERNEL_TEMPLATE_FORMAT_NAME_TYPE, HANDLEBARS_TEMPLATE_FORMAT_NAME_TYPE, JINJA2_TEMPLATE_FORMAT_NAME_TYPE
]
