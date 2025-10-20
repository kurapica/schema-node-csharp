<template>
    <markdown>
        ## 06. Array Types

        Arrays must specify the element type. Due to the nature of forms, arrays of arrays (i.e., nested arrays) are not allowed. This restriction has remained in place for over three years of use in our projects, and it aligns well with relational database table design principles.

        When the array elements are enum values, the array is rendered as a multi-select input. When the elements are of a basic data type, entering one value will automatically generate the next input field. This behavior can be observed in the view used when selecting primary keys for struct arrays.

        If the array elements are structs, a **table view (`tableView`)** is used to display them. For example:

    </markdown>
    <showandtry type="test.subjectscores"></showandtry>
    <markdown>
        You'll notice that when duplicate subjects are selected, an error occurs. This is because the system enforces constraints based on the **primary key fields**, which can be viewed in the type display panel. These fields are used in sequence to validate data uniqueness.

        This primary key configuration is also used to generate unique indexes in the corresponding database table schema.

        If the struct elements within the array contain nested struct arrays, the child arrays will be expanded and displayed inline—but only up to **two levels of nesting** are supported.
    </markdown>
    <showandtry type="test.persons"></showandtry>
    <markdown>
        If you want to present the data in a different format, you can register an alternative visual layout using a statement like `regSchemaTypeView("test.persons", waterFallView, "waterfall")`. After registration, you can specify which view (or "skin") to use for rendering.
    </markdown>
    <showandtry type="test.persons" skin="waterfall"></showandtry>
    <markdown>
        Although the array view includes pagination functionality, the logic for switching pages has not yet been implemented because the backend SDK is not open-sourced at this time. This feature will be added later.
    </markdown>
</template>

<script setup lang="ts">
import markdown from '@/components/markdown.vue'
import showandtry from '@/components/showandtry.vue'
</script>