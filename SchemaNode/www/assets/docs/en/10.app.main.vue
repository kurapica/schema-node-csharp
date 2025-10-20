<template>
    <markdown>
        ### 10.4 Main Application

        Data entry and enabling the data push chain present a challenge in establishing a mutual exclusion lock for data updates. Whether using distributed or local locks, a lock key is required. Typically, this is done in the form {application name}_{target ID}.

        However, if data push involves multiple applications, for example, each application aggregates data for a layer of managers, then a safe approach is to identify a sufficiently high-level manager as the target ID to establish a lock.

        To clarify this step, you can specify a master application for the application (this only applies if the application's fields are reference fields). This allows the mutual exclusion lock to be locked at a higher level, avoiding issues.

        ### TODO

        Further discussion of the application's features awaits further discussion after the release of the basic backend SDK.
    </markdown>
</template>

<script lang="ts" setup>
import markdown from '@/components/markdown.vue'
</script>