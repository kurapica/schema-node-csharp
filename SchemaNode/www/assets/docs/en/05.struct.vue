<template>
    <markdown>
        ## 05. Struct Types

        Struct types are the core of the entire data type system. They define how different fields are combined to form a composite data structure and how those fields relate to one another.

        For example, consider a student grade struct:
    </markdown>
    <showandtry type="test.person"></showandtry>
    <markdown>
        ## 05. Struct Types

        Struct types are the core of the entire data type system. They define composite data types by combining multiple fields and configuring relationships between them.

        For example, consider a student grade struct:

        A struct consists of multiple fields, each of which can be assigned any data type. Depending on the data type, different parameters can be configured. For instance, basic types often support min/max constraints. If a whitelist is configured, the input UI will be replaced with a selection UI.

        Enum-type fields have even more options—root values, cascading constraints, whitelists/blacklists, etc. These can be explored through actual use.

        Field order can be adjusted by dragging and dropping. It's recommended to use lowercase for field names to avoid issues during serialization/deserialization in data transmission.

        ### Common Field Configurations

        1. **Display Name (`display`)**: Used as the label in forms.
        2. **Description (`desc`)**: Supplementary explanation, useful in custom views.
        3. **Required (`require`)**: Marks the field as mandatory for validation.
        4. **Immutable (`immutable`)**: Value can only be entered once and is not editable afterward.
        5. **Read-Only (`readonly`)**: Value is not editable, often set by associated functions.
        6. **Invisible (`invisible`)**: Field is hidden in the UI, commonly used for intermediate data.
        7. **Display Only (`displayOnly`)**: Shown only for display; value is not submitted and no column is generated in the data table. Often used with `invisible` and function results.
        8. **Unit (`unit`)**: Displayed alongside the display name.
        9. **Default (`default`)**: Default value based on the data type.
        10. **Whitelist (`whiteList`)**: For basic or enum types, defines allowed values.
        11. **Blacklist (`blackList`)**: For basic or enum types, defines disallowed values and restricts the whitelist accordingly.
        12. **Lower Limit (`lowLimit`)**: Minimum value. For strings, this refers to length.
        13. **Upper Limit (`upLimit`)**: Maximum value. For strings, this also refers to length. Only strings ≤128 characters can be used as array keys.
        14. **As Suggestion Only (`asSuggest`)**: For basic types, whitelist is used as suggestion rather than restriction.
        15. **Use Original Value for Upper Limit (`useOriginForUpLimit`)**: For scenarios like quota limits where a portion is already used and needs to be considered in the limit calculation.
        16. **Cascade (`cascade`)**: Cascading constraints for enum types (e.g., limiting to county level in administrative regions).
        17. **Root Value (`root`)**: Starting point for enum values, such as displaying provinces only. Can also be used with basic types (e.g., namespace selection).
        18. **Any Level Selectable (`anyLevel`)**: Allows selection from any level of a cascading enum (default is last level only). Useful in scenarios like parameter tables where upper-level enums serve as default values for lower levels.
        19. **Single Flag (`singleFlag`)**: For bitwise enums, restricts selection to a single value (disallows combinations).

        ---

        ### Field Association Configuration

        Below the field list is the **Field Data Association List**, which defines how fields relate to each other. These fields can be from the current struct or deep-nested fields.

        For example, the grade list is an array whose element is a struct. You can associate a whitelist to a field in that struct, such as limiting the subject field based on the subject list. This is done by selecting the relationship type as `Whitelist` and using a function like `=`.

        The available relationship types depend on the selected field’s data type. Once a relationship type is chosen, the return type of the function is determined, and the list of functions is filtered accordingly.

        After selecting a function, the number of parameters is fixed. You can then choose fields or enter fixed values based on parameter types.

        ### Relationship Type Descriptions

        1. **Default (`default`)**: Calculates a default value. If the current value is missing or matches the old default, it will be updated to the new default.
        2. **Root (`root`)**: Sets the root value for enum fields.
        3. **Blacklist (`blackList`)**: Defines forbidden enum values (or base values). When combined with a whitelist, it further restricts selection. In the grade list example, the subject field’s blacklist pulls subjects from all prior elements in the list to prevent duplicate subject selection. This is optional—array elements are also validated against their keys.
        4. **Whitelist (`whiteList`)**: Defines allowed enum or base values.
        5. **Lower Limit (`lowLimit`)**: Sets the lower bound for base type values.
        6. **Upper Limit (`upLimit`)**: Sets the upper bound for base type values.
        7. **Invisible (`invisible`)**: Hides the field when the condition returns true.
        8. **Disable (`disable`)**: Disables input when true.
        9. **Assign (`assign`)**: Forces a new value to be written.
        10. **Init Only (`initOnly`)**: Triggered only during array row creation. For example, setting the creator of a record without affecting past data.
        11. **Type Switch (`type`)**: Allows switching field types. This is risky and should only be used when types are somewhat compatible. For example, switching score values based on whether "credit points" are used. Use with caution.
        12. **Cascade (`cascade`)**: Sets cascading constraints for enum values.
        13. **Single Flag (`singleFlag`)**: Restricts bitwise enums to single selection.

        ---

        By combining field configurations and inter-field associations, dynamic form behaviors can be achieved. However, the current open-source version lacks backend support, so only partial functionality is available. More capabilities will be introduced in future open-source updates.
    </markdown>
</template>

<script setup lang="ts">
import markdown from '@/components/markdown.vue'
import showandtry from '@/components/showandtry.vue'
</script>