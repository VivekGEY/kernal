# Copyright (c) Microsoft. All rights reserved.

from typing import List

from semantic_kernel.diagnostics.verify import Verify
from semantic_kernel.skill_definition.parameter_view import ParameterView


class FunctionView:
    _name: str
    _skill_name: str
    _description: str
    _is_semantic: bool
    _is_asynchronous: bool
    _parameters: List[ParameterView]

    def __init__(
        self,
        name: str,
        skill_name: str,
        description: str,
        parameters: List[ParameterView],
        is_semantic: bool,
        is_asynchronous: bool = True,
    ) -> None:
        Verify.valid_function_name(name)

        self._name = name
        self._skill_name = skill_name
        self._description = description
        self._parameters = parameters
        self._is_semantic = is_semantic
        self._is_asynchronous = is_asynchronous

    @property
    def name(self) -> str:
        return self._name

    @property
    def skill_name(self) -> str:
        return self._skill_name

    @property
    def description(self) -> str:
        return self._description

    @property
    def parameters(self) -> List[ParameterView]:
        return self._parameters

    @property
    def is_semantic(self) -> bool:
        return self._is_semantic

    @property
    def is_asynchronous(self) -> bool:
        return self._is_asynchronous

    def to_fully_qualified_name(self) -> str:
        return f"{self.skill_name}.{self.name}"

    def to_manual_string(self) -> str:
        inputs = "\n\t".join(
            [f"\t\t- {p.name}: {p.description}" for p in self.parameters]
        )
        return f"""
            {self.to_fully_qualified_name()}:
                description: {self.description}
                inputs: 
            {inputs}
        """

    @name.setter
    def name(self, value: str) -> None:
        Verify.valid_function_name(value)
        self._name = value

    @skill_name.setter
    def skill_name(self, value: str) -> None:
        self._skill_name = value

    @description.setter
    def description(self, value: str) -> None:
        self._description = value

    @parameters.setter
    def parameters(self, value: List[ParameterView]) -> None:
        self._parameters = value

    @is_semantic.setter
    def is_semantic(self, value: bool) -> None:
        self._is_semantic = value

    @is_asynchronous.setter
    def is_asynchronous(self, value: bool) -> None:
        self._is_asynchronous = value
