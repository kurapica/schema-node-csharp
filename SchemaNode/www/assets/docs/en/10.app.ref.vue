<template>
    <markdown>
        ### 10.3 Application Field References

        An application data package typically doesn't exist independently. For example, to facilitate management or dynamic integration, a large project data package can be split into multiple smaller application data packages. A main project package provides project-related information, while a sub-data package provides data related to specific functions.

        If a sub-data package needs to access data from the main project package, it can create a field of the corresponding type within itself and reference the main project package's field. This allows the sub-package to easily customize various data associations.

        In addition to data references, you can also create output fields that point to other applications. When entering data, the data itself is also calculated and aggregated before being saved to the data of the other application. However, this involves complex locking logic, so avoid this approach if possible.

        The corresponding entities in the main project package and the sub-data package can be consistent or inconsistent. If they are inconsistent, the backend will need to call a specific API to establish the association.
    </markdown>
</template>

<script lang="ts" setup>
import markdown from '@/components/markdown.vue'
</script>