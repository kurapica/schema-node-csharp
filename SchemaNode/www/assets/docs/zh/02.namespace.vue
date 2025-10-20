<template>
    <markdown>
        ## 02. 命名空间

        命名空间用于对数据类型进行管理，避免同名类型相互冲突，通常来说，可以采用类似 `组织名.应用名.功能.类型` 之类形式进行组织。

        可以在右侧的类型管理中点击新增后，类型选择默认的命名空间进行尝试。

        系统默认带有`system`命名空间，提供基本的数据类型，以及通常函数。

        配置界面中可以看到一个`schema`命名空间，它定义的类型用于实现类型定义，类似`schema.valuetype`可以用于选择类型。

        `system`和`schema`命名空间都被记录为系统级，所以它们不可被修改，`schema`仅限于本服务使用，请勿使用它的类型。

        请尽可能使用小写作为命名空间名称。
    </markdown>
</template>

<script lang="ts" setup>
import markdown from '@/components/markdown.vue'
</script>