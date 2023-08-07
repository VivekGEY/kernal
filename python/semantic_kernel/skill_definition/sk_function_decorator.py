# Copyright (c) Microsoft. All rights reserved.


def sk_function(
    *,
    description: str = "",
    name: str = "",
    input_description: str = "",
    input_default_value: str = "",
    is_function_call: bool = False,
):
    """
    Decorator for SK functions.

    Args:
        description -- The description of the function
        name -- The name of the function
        input_description -- The description of the input
        input_default_value -- The default value of the input
        is_function_call -- Whether the function is a function call
    """

    def decorator(func):
        func.__sk_function__ = True
        func.__sk_function_description__ = description or ""
        func.__sk_function_name__ = name or func.__name__
        func.__sk_function_input_description__ = input_description or ""
        func.__sk_function_input_default_value__ = input_default_value or ""
        func.__sk_function_is_function_call__ = is_function_call or False
        return func

    return decorator
