<template>
    <markdown>
        ### 10.2 Field Data Push

        The field definition mentions a calculation function. The field using the calculation function is the output field. The calculation function determines the calculation rules, and its parameters indicate the input fields.

        On the front-end interface, only the input fields are typically presented to assist in data entry. After submission, the back-end not only saves the entered data in the database according to the field definition but also checks whether it is an input field for other fields. If so, the corresponding calculation function is called to process it.

        The processed data is then merged into the data in the output field according to the specified data merging rules. Furthermore, an output field can serve as a parameter for other output fields, thus forming a data push chain. Data processing and aggregation are completed during data entry.
    </markdown>
</template>

<script lang="ts" setup>
import markdown from '@/components/markdown.vue'
</script>