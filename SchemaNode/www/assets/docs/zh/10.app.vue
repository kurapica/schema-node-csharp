<template>
    <markdown>
        ## 10. 应用

        前面描述的都是数据类型的定义，实际项目中，我们会围绕一个实体进行数据组织，例如用户，或者项目。
        
        围绕一个实体，可以给定一组字段，每个字段都具有指定的类型，因此可以基于数据类型完成数据录入和存储。
    </markdown>
    <showandtryapp app="test"></showandtryapp>
</template>

<script lang="ts" setup>
import markdown from '@/components/markdown.vue'
import showandtryapp from '@/components/showandtryapp.vue';
</script>