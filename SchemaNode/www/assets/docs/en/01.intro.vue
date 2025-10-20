<template>
    <section>
        <markdown>
            # Schema-Node Data Node System

            ## 01. Introduction

            The core function of this system is to describe data types using the JSON format. These data types include basic types, enum types, struct types, array types, and function types.

            In addition to conventional data types, to enable unified operations on both frontend and backend, function types are defined using JSON descriptions and can be compiled into executable functions for both ends. Functions that cannot be fully described in JSON can still be declared, and their implementations can reside only on the backend. The frontend can recognize such functions and decide whether to execute them locally or invoke backend services.

            Data nodes are created on the frontend based on these data types. These nodes handle data organization, storage, validation, and interaction. Data types can be linked together, and with function types and other nodes, enable interactivity such as modifying default values, setting blacklists/whitelists, determining availability, or altering data types based on function evaluation.

            You can click the button below to try it out. After selecting a subject, the corresponding subject score list will appear. The whitelist of this list is determined by the selected subject, and the blacklist is determined by subjects that already have scores configured. These behaviors are configured through the data association list within each type's configuration. Clickable green types can be explored further.

        </markdown>
        <showandtry type="test.person"></showandtry>
        <br/>
        <el-collapse accordion>
            <el-collapse-item title="Open Source Contents in This Release">
                <markdown>
                    This release includes only the frontend-specific parts with backend dependencies removed. It includes two libraries and one frontend type configuration manager.

                    1. [schema-node](https://github.com/kurapica/schema-node): A dependency-free data node system based on data types. It creates data nodes and enables data subscription based on defined relationships.

                    2. [schema-node-vueview](https://github.com/kurapica/schema-node-vueview): A Vue 3 + Element Plus-based data node view library. Different views can be registered for different data types. This library provides the default implementations.

                    3. [schema-node-man](https://github.com/kurapica/schema-node-man): A frontend-only type configuration manager. It supports uploading and downloading custom types with automatic caching. Once downloaded, the types can be used with the above two libraries in any project.
                </markdown>
            </el-collapse-item>
            <el-collapse-item title="Application and Server Architecture (Not Included in This Release)">
                <markdown>
                    When considering how to customize an application based on data types, each application's entity (e.g., a user) should have a unique application ID. In the database, such entities might be stored across multiple tables. Each table field corresponds to a part of the data type structure.

                    For example, you could assign a `struct` type to store user information—each user would correspond to one row. For a report card or grade list, an `array` type would represent multiple rows.

                    In this way, an application can be defined with multiple fields, each associated with a specific data type. The system can automatically generate the database table structure based on these configurations.

                    When the client requests application data by application ID, the server can package the required type information, enum values (lazy-loaded if needed), and the data itself to reduce access pressure.

                    Clients can use a unified API to submit changes to the application data. Besides just saving, fields can be associated to form data push chains—often used for filtering, transformation, and aggregation—so results can be prepared without needing to process raw data after collection. This is especially efficient when handling large volumes of data.

                    If a field is declared as a broadcast type, it can trigger protocol-specific broadcast operations after data calculation and pushing.

                    The server architecture can be divided into configuration management servers and application servers. Configuration servers are internal-only. Once updated, they can push changes to all application servers, which will then automatically update database schemas and related logic online.

                    ![Architecture Diagram](./assets/img/architecture.en.png)
                </markdown>
            </el-collapse-item>
        </el-collapse>
    </section>
</template>

<script setup lang="ts">
import markdown from '@/components/markdown.vue'
import showandtry from '@/components/showandtry.vue'
</script>