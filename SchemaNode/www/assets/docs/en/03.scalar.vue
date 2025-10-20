<template>
    <markdown>
        ## 03. Basic Data Types

        Basic data types are unstructured types and are stored in the database as the smallest units of data. Typically, custom basic types are not allowed, but you can define new types based on existing basic types. However, if the goal is simply to configure constraints like minimum or maximum values, it's better to define them within a struct.

        In some special cases, custom definitions are necessary. For example, in this configuration service, we define `schema.valuetype`, which is based on the string type but has a custom view registered for it. This allows it to be used for selecting data types. Note that it is only used for selecting data types—not for selecting namespaces or functions.

        Fields declared with this type can directly use the registered view for interaction. This includes use cases like function return types, struct field types, and more.
    </markdown>
    <showandtry type="schema.valuetype"></showandtry>
    <markdown>
        If you pay attention to how the type is displayed, you’ll notice that we actually use the `schema.scalardefine` type to describe a basic data type. You can try it out briefly just to get a sense of how it works—but in practical applications, there is rarely a need to define additional basic data types.
    </markdown>
    <showandtry type="schema.scalardefine"></showandtry>
</template>

<script setup lang="ts">
import markdown from '@/components/markdown.vue'
import showandtry from '@/components/showandtry.vue'
</script>