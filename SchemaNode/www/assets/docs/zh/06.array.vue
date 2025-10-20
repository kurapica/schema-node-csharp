<template>
    <markdown>
        ## 06. 数组类型

        数组需要指定元素类型，因为表单的性质，不允许指定数组为元素。目前项目中使用累计三年，这个限制没被打破，这样也符合数据库表结构设计。

        数组元素为枚举值时，表现为多选，为基础数据时，填入一个会出现下一个输入框，可以参考为结构体数组选择主键时的视图。

        如果数组元素为结构体，会使用表视图(tableView)进行展示，例如
    </markdown>
    <showandtry type="test.subjectscores"></showandtry>
    <markdown>
        不难发现，当学科选择重复时，会有报错，可以查看类型展示中的作为主键的字段列表，它会按照顺序对数据进行约束。这个主键配置也会作为数据表表结构的唯一索引被生成。

        如果结构体元素中具有数组结构体，它会将子数组结构体展开显示，但仅限于二级数组。
    </markdown>
    <showandtry type="test.persons"></showandtry>
    <markdown>
        如果需要以其它形式呈现，可以通过`regSchemaTypeView("test.persons", waterFallView, "waterfall")`的方式注册不同的皮肤视图，然后指定使用。
    </markdown>
    <showandtry type="test.persons" skin="waterfall"></showandtry>
    <markdown>
        数组视图虽然添加了分页功能，但因为后端SDK尚未开源，分页切换逻辑暂未加入，稍后引入。
    </markdown>
</template>

<script setup lang="ts">
import markdown from '@/components/markdown.vue'
import showandtry from '@/components/showandtry.vue'
</script>