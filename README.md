# ODataApiDoc
This app generates documentation of the [sensenet](https://github.com/sensenet "sensenet on github") API.
Extracts the XML code documentation from csharp (.cs) source files and creates markdown (.md) files in special hardcoded format.
The documentation is created two different format for frontend and backend developers.

***NOTE that this app is [sensenet](https://github.com/sensenet "sensenet on github") API specific!***

Only sensenet's OData operation methods are exported. The following conditions should be met:
- The API element is a method
- The method is public
- The method is static
- The method has ODataAction or ODataFunction attribute.
- The type of the first parameter is SenseNet.ContentRepository.Content

The app focused the modern production code so skips test projects and .NET Framework projects by default.

The app recognizes and interprets most of the XML code documentation elements. The following elements are important: summary, remarks, para, exception, param, paramref, example, returns, c, code, see, seealso.

The app recognizes and interprets these C# attributes: ODataAction, ODataFunction, ContentTypes, AllowedRoles, RequiredPermissions, RequiredPolicies, Scenario, OperationName, Description, Icon.

## Usage
```text
ODataApiDoc <InputDir> <OutputDir> [-cat|-op|-flat] [-all]
```
- **InputDir**: source code input. Can be one project or repository but the app can process a whole ecosystem if it is in one directory (but consider that more files more processing time).
- **OutputDir**: documentation output. Contains "backend" and "frontend" directories and a "generation.txt" files. *Warning: when the app starts, the output directory will be deleted if exists and re-created.*
- File structure control
  - **cat**: every category is a single file containing all operations.
  - **op**: every category is a directory that contains the operations: one operation per file.
  - **flat**: every operation is one file in the root directory (category is only in the file).
- **all**: the app includes test projects and .NET Framework projects too.

## generation.txt
This file contains additional information about documentation generation:
- Input directory
- Operation count
- Missing documentation section: File and method and the list of the missings: not documented parameters, &lt;summary> or &lt;return>.
- Operation descriptions section: values of the description parameter in the operation attributes. The document generator does not use these texts.
- Functions and parameters section: OData function header list.
- Actions and parameters section: OData action header list.
- Cheat sheet section: simplified and categorized list of all operations. The first section contains the undocumented API elements. This section is different from the "Uncategorized" category.

## index.md
The "backend" and "frontend" directories contain an index.md that is the sorted easy readable linked list of all operations in the format for the matching role.

*NOTE that the "Index" category is renamed to "ndex" for technical reasons.*

## Code documentation extensions
There are some new XML elements that help fine-tune documentation generation.
### &lt;snCategory>
Simple category of the operation. One operation one term. The term can contain whitespace.
### &lt;nodoc>
Text inside this node is omitted in the documentation. It is useful when a sentence is important for the code writers but undesirable (or noisy) in the documentation.
### &lt;param name="" example=''>
Value of the "example" attribute will be the example code of the given parameter.