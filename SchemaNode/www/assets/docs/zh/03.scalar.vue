<template>
    <markdown>
        ## 03. 基础数据类型

        基础数据类型是无结构的数据类型，作为最小的数据单元被数据库保存。通常不允许自己定义基础数据类型，但可以基于已有的基础数据类型进行定义，但如果只是为了配置例如最大最小值的情况，可以在结构体中进行配置。

        但一些特殊情况，需要定义，例如在本配置服务中，我们定义了`schema.valuetype`，它基于字符串类型，然后为它注册了特殊的视图，这样就可以基于这个类型完成数据类型选择。当然，它只用于选择数据类型，不能用于选择命名空间和函数。

        这样申明使用该类型的字段，都可以直接使用注册的视图完成操作，例如函数返回值类型，结构体字段类型等等。
    </markdown>
    <showandtry type="schema.valuetype"></showandtry>
    <markdown>
        如果注意到类型展示中它的展示形式的话，实际我们用`schema.scalardefine`类型来描述一个基础数据类型。可以简单尝试下，了解即可，实际应用中很少需要额外定义基础数据类型。
    </markdown>
    <showandtry type="schema.scalardefine"></showandtry>
</template>

<script setup lang="ts">
import markdown from '@/components/markdown.vue'
import showandtry from '@/components/showandtry.vue'
</script>