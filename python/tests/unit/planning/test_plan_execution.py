import semantic_kernel as sk

from pytest import mark, raises
from semantic_kernel.planning import Plan

from semantic_kernel.core_skills.text_skill import TextSkill

def test_invoke_empty_plan():
    plan = Plan()
    result = plan.invoke()
    assert result.result == ""

def test_invoke_plan_constructed_with_function():
    # create a kernel
    kernel = sk.Kernel()

    # import test (text) skill
    skill = TextSkill()
    skill_config_dict = kernel.import_skill(skill, "text")
    test_function = skill_config_dict["uppercase"]

    # setup context
    context = kernel.create_new_context()
    context["input"] = "hello world "

    plan = Plan(name="test", function=test_function)
    result = plan.invoke(context=context)
    assert result.result == "HELLO WORLD "

def test_invoke_empty_plan_with_added_function_step():
    # create a kernel
    kernel = sk.Kernel()

    # import test (text) skill
    skill = TextSkill()
    skill_config_dict = kernel.import_skill(skill, "text")
    test_function = skill_config_dict["uppercase"]

    # setup context
    context = kernel.create_new_context()
    context["input"] = "hello world "

    plan = Plan(name="test")
    plan.add_steps([test_function])
    result = plan.invoke(context=context)
    assert result.result == "HELLO WORLD "

def test_invoke_empty_plan_with_added_plan_step():
    # create a kernel
    kernel = sk.Kernel()

    # import test (text) skill
    skill = TextSkill()
    skill_config_dict = kernel.import_skill(skill, "text")
    test_function = skill_config_dict["uppercase"]

    # setup context
    context = kernel.create_new_context()
    context["input"] = "hello world "

    plan = Plan(name="test")
    new_step = Plan(name="test", function=test_function)
    plan.add_steps([new_step])
    result = plan.invoke(context=context)
    assert result.result == "HELLO WORLD "

def test_invoke_multi_step_plan():
    # create a kernel
    kernel = sk.Kernel()

    # import test (text) skill
    skill = TextSkill()
    skill_config_dict = kernel.import_skill(skill, "text")
    test_function = skill_config_dict["uppercase"]
    test_function2 = skill_config_dict["trim_end"]

    # setup context
    context = kernel.create_new_context()
    context["input"] = "hello world "

    plan = Plan(name="test")
    new_step = Plan(name="test", function=test_function)
    new_step2 = Plan(name="test", function=test_function2)
    plan.add_steps([new_step, new_step2])
    result = plan.invoke(context=context)
    assert result.result == "HELLO WORLD"