<template>
    <markdown>
        ## 04. Enum Values

        Enum values are a commonly used data type, with the following characteristics:

        1. Data types: integer, numeric, string, or bitwise flags  
        2. Whether cascading (multi-level enum values) is enabled  
        3. Whether multiple selections are allowed  
        4. Whether bitwise enum values must be mutually exclusive (e.g., cannot select 1 + 2)  
        5. The starting value for cascading enums  
        6. Whitelist support  
        7. Blacklist support  
        8. Maximum display depth for cascading enums (e.g., in administrative regions, only show down to the county level)

        When defining enum values, you only need to focus on points 1 and 2. The rest are configured within the struct field where the enum is used. Later, you'll also see how to configure these dynamically using associations and functions.
    </markdown>
    <showandtry type="test.subject"></showandtry>
    <markdown>
        You can create your own enum values under the `test` namespace. Be sure to select both the enum data type and the cascading enum name—the former determines the type of data that can be entered, while the latter defines how many levels of enum values are supported.

        Note: When adding new enum values, you won't be able to input sub-values immediately. Please save and reopen the enum to continue editing sub-level values.

        Regarding feature **3. Multiple Selection**: for non-bitwise enum values, you need to create an array type based on the enum. This array type will provide a multi-select view for the enum values.

    </markdown>
    <showandtry type="test.subjects"></showandtry>
    <markdown>
        If an enum is declared as a bitwise (flag) enum, it will be treated as multi-select by default—unless it is explicitly specified as a single selection when used in a struct.
    </markdown>
    <showandtry type="test.role"></showandtry>
</template>

<script setup lang="ts">
import markdown from '@/components/markdown.vue'
import showandtry from '@/components/showandtry.vue'
</script>