<template>
    <markdown>
        ## 09. Custom Views

        To create custom views, you can refer to the default implementations provided in `schema-node-vueview`. There are a few important points to keep in mind:

        1. In Vue, the `node` data node is passed into the component via a prop with a fixed name. Once received, you should use `toRaw` to unwrap it into the actual data node instance. Otherwise, you’ll get a Vue-wrapped reactive object, which may cause issues during further processing.
    </markdown>
    <pre>
        <code> 
            const props = defineProps<{ node: ScalarNode }>()
            const scalarNode = toRaw(props.node)
        </code>
    </pre>
    <markdown>
        2. Avoid using `watch` to observe changes in the node’s data or state. Instead, use the subscription methods provided by the node itself for more accurate and efficient tracking.
    </markdown>
    <pre>
        <code>
            // declare the state
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
                // subscribe data change
                dataWatcher = scalarNode.subscribe(() => {
                    const data = scalarNode.rawData // use rawData to allow error data
                    state.data = data
                    state.display = `${!isNull(data) ? data : ''}`
                }, true)

                // subscribe state change, like invisible
                stateWatcher = scalarNode.subscribeState(() => {
                    state.disable = scalarNode.rule.disable
                    state.require = scalarNode.require
                    state.readonly = scalarNode.readonly
                }, true)
            })

            // dispose subscription
            onUnmounted(() => {
                if (dataWatcher) dataWatcher()
                if (stateWatcher) stateWatcher()
            })
        </code>
    </pre>
    <markdown>
        3. Once the custom view is defined, there are two ways to register it for a specific type.
    </markdown>
    <pre>
        // override the default view
        regSchemaTypeView("test.persons", waterFallView)

        // register view with skin, use `skin` in schemaview to active the skin view
        regSchemaTypeView("test.persons", waterFallView, "waterfall")
    </pre>
</template>

<script lang="ts" setup>
import markdown from '@/components/markdown.vue'
</script>