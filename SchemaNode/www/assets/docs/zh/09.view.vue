<template>
    <markdown>
        ## 09.自定义视图

        自定义视图定制可以参考`schema-node-vueview`中的默认实现，需要注意几点。

        1. vue中通过定义props传入node节点，名字是固定，拿到后，需要通过toRaw转为实际节点，否则拿到的是vue封装过的，后续处理会有问题。
    </markdown>
    <pre>
        <code> 
            const props = defineProps<{ node: ScalarNode }>()
            const scalarNode = toRaw(props.node)
        </code>
    </pre>
    <markdown>
        2. 请不要使用watch来订阅节点的数据修改和状态修改，可以通过节点的方法来订阅。
    </markdown>
    <pre>
        <code>
            // 申明模板需要的状态
            const state = reactive<{
                data?: any,
                display?: any,
                disable?: boolean,
                require?: boolean,
                readonly?: boolean,
            }>({})

            // data & state watcher
            let dataWatcher: Function | null = null
            let stateWatcher: Function | null = null

            onMounted(() => {
                // 订阅数据变更
                dataWatcher = scalarNode.subscribe(() => {
                    const data = scalarNode.rawData // data可能会基于类型定义被转换，使用rawData可以显示错误的数据
                    state.data = data
                    state.display = `${!isNull(data) ? data : ''}`
                }, true)

                // 订阅节点的状态变化，类似不可见，黑白名单变更等状态变化
                stateWatcher = scalarNode.subscribeState(() => {
                    state.disable = scalarNode.rule.disable
                    state.require = scalarNode.require
                    state.readonly = scalarNode.readonly
                }, true)
            })

            // 销毁时，同时销毁订阅记录
            onUnmounted(() => {
                if (dataWatcher) dataWatcher()
                if (stateWatcher) stateWatcher()
            })
        </code>
    </pre>
    <markdown>
        3. 定义完成后，可以通过两种方式为类型注册。
    </markdown>
    <pre>
        // 覆盖默认视图
        regSchemaTypeView("test.persons", waterFallView)

        // 为类型注册皮肤名为waterfall的视图,之后可以在schemaView中通过skin指定
        regSchemaTypeView("test.persons", waterFallView, "waterfall")
    </pre>
</template>

<script lang="ts" setup>
import markdown from '@/components/markdown.vue'
</script>