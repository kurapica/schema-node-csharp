<template>
    <markdown>
        ## 02. Namespace

        Namespaces are used to manage data types and avoid naming conflicts between types with the same name. Typically, namespaces can follow a structure like `organization.application.feature.type`.

        You can try this by clicking "Add" in the type management section on the right and selecting the default namespace for your new type.

        The system includes a built-in `system` namespace, which provides basic data types and commonly used functions.

        In the configuration interface, you will also see a `schema` namespace. This namespace defines types used for type definitions themselves—for example, `schema.valuetype` can be used to select a type.

        Both the `system` and `schema` namespaces are marked as system-level, so they cannot be modified. The `schema` namespace is reserved for internal use by this service, and its types should not be used directly.

        It is recommended to use lowercase letters for namespace names whenever possible.
    </markdown>
</template>

<script lang="ts" setup>
import markdown from '@/components/markdown.vue'
</script>