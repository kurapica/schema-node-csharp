<template>
    <markdown>
        ## 08. Usage

        The data node library itself does **not depend on Vue or React**, so it can be integrated with any suitable data view library to enable automatic rendering on the frontend.

        In this release, we provide `schema-node-vueview`, a data view library built with **Vue 3** and **Element Plus**. Below is an introduction to how to use it:

        1. Create a demo project and initialize it, selecting **Vue** and **TypeScript**.
    </markdown>
    <pre>
        npm create vite@latest schema-node-demo -- --template vue-ts
        cd schema-node-demo
        npm install
    </pre>
    <markdown>
        2. Install the dependencies: `element-plus`, `schema-node-vueview`, and `schema-node`.
    </markdown>
    <pre>
        npm install schema-node
        npm install element-plus
        npm install schema-node-vueview
    </pre>
    <markdown>
        3. In the configuration manager, choose to download the `test` namespace. Copy the downloaded `test.json` file to `schema-node-demo/assets/test.json`.

        4. Open `schema-node-demo/main.ts` and add the import and registration statements.
    </markdown>
    <pre>
        import { createApp } from 'vue'
        import './style.css'
        import App from './App.vue'
        import ElementPlus from "element-plus"
        import 'element-plus/dist/index.css'

        // import the downlaod json and register them
        import schema from './assets/test.json'
        import { registerSchema, type INodeSchema } from 'schema-node'
        registerSchema(schema as INodeSchema[])

        const app = createApp(App)
        app.use(ElementPlus)
        app.mount('#app')
    </pre>
    <markdown>
        5. Open `schema-node-demo/App.vue` and update its content as follows:
    </markdown>
    <pre>
       <code>
            &lt;script setup lang="ts"&gt;
                import { schemaView } from 'schema-node-vueview'
                import { ref } from 'vue'

                // binding data
                const data = ref(null)
            &lt;/script&gt;

            &lt;template&gt;
                &lt;el-form label-width="140px" :model="data" style="width:600px"&gt;
                    &lt;!-- use shcema-view for all schema nodes --&gt;
                    &lt;schema-view
                        v-model="data"
                        :in-form="true"
                        :config="{
                            type: 'test.person',
                            display: 'Info'
                        }"
                    &gt;&lt;/schema-view&gt;
                &lt;/el-form&gt;
                &lt;pre&gt;&#123;&#123; data &#125;&#125;&lt;/pre&gt;
            &lt;/template&gt;

            &lt;style lang="css"&gt;
            body {
                background-color: white;
                color: black
            }
            &lt;/style&gt;
       </code>
    </pre>
    <markdown>
        6. Save the files and run `npm run dev` to see the result.

        ---

        `schemaView` is a general-purpose data node view component. It renders a view based on the specified node type and **skin**, selecting the appropriate visual representation. The `inForm` attribute indicates that the view is being used within a form, enabling automatic validation.

        You can also manually trigger validation by calling the `form.validate()` method.

        In summary, you can upload existing type configurations through the configuration interface, modify them as needed, and download them to replace your local files. This makes maintenance quite convenient. Additionally, since the types are cached in the browser, it supports long-term editing and persistence.

        Once a type has been registered, you can also create data nodes directly for use. In this case, you can update the contents of `App.vue` with something like the following:
    </markdown>
    <pre>
        <code>
            &lt;script setup lang="ts"&gt;
            import { type AnySchemaNode, getSchemaNode } from 'schema-node';
            import { schemaView } from 'schema-node-vueview'
            import { onMounted, ref } from 'vue'

            const node = ref&lt;AnySchemaNode | null&gt;(null)
            onMounted(async () =&gt; {  
            node.value = await getSchemaNode({ type: "test.person" }, {})
            })
            &lt;/script&gt;

            &lt;template&gt;
            &lt;el-form v-if="node" label-width="140px" :model="node.data" style="width:600px"&gt;
                &lt;schema-view
                :node="(node as AnySchemaNode)"
                :in-form="true"
                &gt;&lt;/schema-view&gt;
            &lt;/el-form&gt;
            &lt;/template&gt;

            &lt;style lang="css"&gt;
            body {
            background-color: white;
            color: black
            }
            &lt;/style&gt;
        </code>
    </pre>
    <markdown>
        You can create a data node by calling the `getSchemaNode` method, specifying the type and initial data. After creation, you can access the data to be submitted via `node.data`, or assign new data by setting `node.data = value`.

        Different types of data nodes provide different methods, though the number of methods is quite small and easy to explore. For example, array-type nodes support `addRow` and `delRows` for adding and removing rows.

        In most cases, you don’t need to handle these methods directly unless you're registering custom views.
    </markdown>
</template>

<script setup lang="ts">
import markdown from '@/components/markdown.vue'
</script>