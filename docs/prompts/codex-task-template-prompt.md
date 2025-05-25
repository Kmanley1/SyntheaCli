# Codex Prompt Template

## Instructions:
Use this template to craft prompts for code-related tasks. Fill in each section and remove the placeholders and examples.

## Template Outline:
**Background/Context:** *(Describe any necessary context or code needed for the task. Include code snippets if relevant, enclosed in triple backticks with language tags for clarity.)*

**Task/Goal:** *(Clearly state the primary objective, e.g., "Generate X", "Explain Y code", "Debug the error in Z", etc.)*

**Specific Requirements:** *(List any specific requirements or constraints, e.g., performance goals, libraries to use/avoid, output formatting, coding style guidelines.)*

**Examples (if any):** *(Provide input-output examples or references if helpful. This guides the model. Use triple backticks for code or input/output examples.)*

**Output Format:** *(Describe the desired format of the response: e.g., "Provide only the code", "Code followed by explanation", "Output as JSON", etc.)*

**Additional Instructions:** *(Any final instructions such as "Include comments in code" or "Avoid using recursion", etc.)*

## Example Scenarios:

### Example 1: Code Generation (C#)
**Background/Context:** We have a list of user objects and need to filter active users and sort them by registration date.  
**Task/Goal:** Write a C# function `GetActiveUsersSorted(List<User> users)` that returns the active users sorted by their `RegistrationDate`.  
**Specific Requirements:** Use LINQ for filtering and sorting. Assume `User` has properties `IsActive` (bool) and `RegistrationDate` (DateTime).  
**Examples:** *(No specific examples provided, straightforward logic.)*  
**Output Format:** Provide only the C# code for the function, enclosed in a Markdown code block with language identifier.  
**Additional Instructions:** Include necessary `using` statements if any.

### Example 2: Code Explanation (PowerShell)
**Background/Context:** A PowerShell script is provided that automates user account creation.  
**Task/Goal:** Explain what the following PowerShell script does, step by step.  
**Specific Requirements:** The explanation should be in clear, non-technical language for a junior IT audience.  
**Examples:** *(The script is provided in the context below.)*  
```
<PowerShell script snippet here>
```  
**Output Format:** The answer should be a few paragraphs explaining the script's logic (not just a list of commands).  
**Additional Instructions:** If certain commands or parameters are unusual, briefly describe their purpose.

### Example 3: Debugging (T-SQL)
**Background/Context:** A T-SQL query intended to calculate total sales is returning incorrect results.  
**Task/Goal:** Identify and fix the bug in the T-SQL query below.  
**Specific Requirements:** The query uses an INNER JOIN which might be causing row duplication. Suggest a fix (e.g., using `SUM(DISTINCT ...)` or adjusting the JOIN).  
**Examples:** *(Problematic query provided below.)*  
```
<SQL query snippet here>
```  
**Output Format:** Provide the corrected T-SQL query and a short explanation of the change.  
**Additional Instructions:** Only modify the necessary part of the query; do not rewrite the entire query.

### Example 4: Refactoring (C# .NET)
**Background/Context:** A C# method is too long and has repeated code.  
**Task/Goal:** Refactor the `ProcessRecords(List<Record> records)` method for better readability and maintainability.  
**Specific Requirements:** - Break the method into smaller, focused functions if needed.  
- Eliminate duplicate code by reusing logic.  
- Ensure no change in external behavior.  
**Examples:** *(Original method is given below.)*  
```
<C# method snippet here>
```  
**Output Format:** Provide the refactored C# code in a code block, followed by a brief explanation of the improvements.  
**Additional Instructions:** Preserve any original comments and ensure the refactored code passes all original tests.
