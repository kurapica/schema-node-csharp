<template>
    <markdown>
        ## 08. 使用

        数据节点库本身并不依赖vue或者react，所以可以配合合适的数据视图库实现前端自动渲染。

        本次提供的schema-node-vueview是基于vue3和ElementPlus构建的数据视图库。先介绍它的使用方式。

        1. 创建一个demo项目，并初始化，选择Vue和TypeScript。
    </markdown>
    <pre>
        npm create vite@latest schema-node-demo -- --template vue-ts
        cd schema-node-demo
        npm install
    </pre>
    <markdown>
        2. 安装element-plus, schema-node-vueview, schema-node依赖。
    </markdown>
    <pre>
        npm install schema-node
        npm install element-plus
        npm install schema-node-vueview
    </pre>
    <markdown>
        3. 配置管理器中选择下载test命名空间，将下载test.json拷贝到`schema-node-demo\assets\test.json`。

        4. 打开`schema-node-demo\main.ts`，在里面引入注册语句。
    </markdown>
    <pre>
        import { createApp } from 'vue'
        import './style.css'
        import App from './App.vue'
        import ElementPlus from "element-plus"
        import 'element-plus/dist/index.css'

        // 导入下载的JOSN类型配置信息并注册
        import schema from './assets/test.json'
        import { registerSchema, type INodeSchema } from 'schema-node'
        registerSchema(schema as INodeSchema[])

        const app = createApp(App)
        app.use(ElementPlus)
        app.mount('#app')
    </pre>
    <markdown>
        5. 打开`schema-node-demo\App.vue`，修改内容为
    </markdown>
    <pre>
       <code>
            &lt;script setup lang="ts"&gt;
                import { schemaView } from 'schema-node-vueview'
                import { ref } from 'vue'

                // 绑定数据
                const data = ref(null)
            &lt;/script&gt;

            &lt;template&gt;
                &lt;el-form label-width="140px" :model="data" style="width:600px"&gt;
                    &lt;!-- 使用schemaView组件进行统一展示 --&gt;
                    &lt;schema-view
                        v-model="data"
                        :in-form="true"
                        :config="{
                            type: 'test.person',
                            display: '信息'
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
        6. 保存文件，然后执行`npm run dev`查看效果。

        ----

        `schemaView`是通用的数据节点视图，它会基于指定的节点类型和皮肤skin，来选取实际的视图进行展示。其中inForm属性用于表明在Form表单中使用，它会实现自动校验功能。

        同样可以调用`form.validate()` 方法触发校验。

        综上所述，可以在本配置界面上传已有的类型配置，修改后，下载到本地覆盖使用即可。维护较为方便。同时，因为类型会缓存在本地浏览器，方便持久维护。

        已经注册的类型，也可以直接创建出数据节点，来使用，这时可以替换`App.vue`内容类似
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
        通过getSchemaNode方法可以指定类型和初始数据来创建，之后可以通过`node.data` 来获取待提交的数据，也可以通过`node.data = value`写入。

        不同的数据节点有不同的方法，不过数量不多，可以自行尝试，类似数组类型可以通过`addRow`和`delRows`添加和删除列。但无需注册自定义视图的场合无需处理。
    </markdown>
</template>

<script setup lang="ts">
import markdown from '@/components/markdown.vue'
</script>