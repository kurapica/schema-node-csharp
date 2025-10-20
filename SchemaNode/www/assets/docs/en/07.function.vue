<template>
    <markdown>
        ## 07. Function Types

        For data nodes, the most important aspects of a function are its **parameters and return value**. These allow us to effectively associate and link different data nodes. The actual implementation of the function is often not a concern—especially for functions like retrieving weather information, which cannot be fully described using JSON.

        The common approach is to register a set of functional utilities on the server, which can then be selected in the type configuration or referenced directly in other JSON-defined functions. The frontend determines whether to execute the function locally or invoke the backend service based on the function's flags and expression definitions.

        Regular functions implement their logic through a list of configured expressions. Typically, if the function returns a single value, the **last expression** is treated as the return value by default. You'll notice that this final expression is highlighted in blue. If the return type of the expression does not match the expected return type of the function, it will be highlighted in red.

    </markdown>
    <showandtry type="test.nosubjects"></showandtry>
    <markdown>
        For functions that need to return a struct, you only need to ensure that the parameter names (or the names used in expressions) match the field names of the target struct. 

        For example, in the case below, it's sufficient to simply combine the parameters to form the return value.
    </markdown>
    <showandtry type="test.gensubjectscore"></showandtry>
    <markdown>
        When defining a function, you can test it by entering input data on the right side to see the calculated result.

        Because it is difficult—or overly complex—to represent loops in JSON, expressions are designed to follow a pure functional approach. Even conditional logic like `if` statements is implemented through function calls.

        A special case is **looping over arrays**. To allow reuse of functions that operate on single elements, the execution mode of an expression can be configured:

        1. **Call (`call`)**: Standard function call.
        2. **Map (`map`)**: Transforms each element of an array using a function and returns a new array of results. The function must be designed to handle a single element, and one of its parameters should be mapped to the array.
        3. **Reduce (`reduce`)**: Aggregates an array by passing its elements and an initial value through a function in sequence. Each result becomes the input for the next iteration. Commonly used for tasks like summing values.
        4. **First (`first`)**: Returns the first element in the array for which the function returns `true`.
        5. **Last (`last`)**: Same as `first`, but processes the array in reverse and returns the last matching element.
        6. **Filter (`filter`)**: Filters the array by applying a function to each element and returning a new array with only those elements for which the function returns `true`.

        You can experiment with these modes. All available options during function editing are dynamically generated based on the relevant data types.
    </markdown>
</template>

<script setup lang="ts">
import markdown from '@/components/markdown.vue'
import showandtry from '@/components/showandtry.vue'
</script>