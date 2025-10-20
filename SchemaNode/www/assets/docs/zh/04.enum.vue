<template>
    <markdown>
        ## 04. 枚举值

        枚举值是比较常用的数据类型，它的特征有:
        1. 数据类型: 整数，数值，字符串，标志位
        2. 是否级联，具有多级枚举值
        3. 是否多选
        4. 标志位枚举值是否单选(不能选择 1 + 2)
        5. 级联枚举值的起始枚举值
        6. 白名单
        7. 黑名单
        8. 级联枚举值的最大显示层级，例如行政区划仅显示到县级

        定义枚举值时，仅需要关心其中的1，2两点，其它部分在使用时的结构体字段中进行配置，后续还可以看到如何基于关联，利用函数来进行配置。
    </markdown>
    <showandtry type="test.subject"></showandtry>
    <markdown>
        查看后，可以在`test`命名空间自行创建枚举值，注意选择枚举值数据类型，和级联枚举名，前者决定可输入的数据，后者决定有多少级。

        另外，新输入的枚举值无法输入下级枚举值，请保存后重新打开再进行操作。

        关于特征`3.是否多选`，对于非标志位枚举值，我们需要创建基于枚举值的数组类型，它会以枚举值多选的形式提供视图。
    </markdown>
    <showandtry type="test.subjects"></showandtry>
    <markdown>
        如果申明为标志位枚举值，则默认就是多选，除非在结构体使用时，指明是单枚举值。
    </markdown>
    <showandtry type="test.role"></showandtry>
</template>

<script setup lang="ts">
import markdown from '@/components/markdown.vue'
import showandtry from '@/components/showandtry.vue'
</script>