# DRYDetective - Automatically refactor DRY violations

## About
This project aims to provide a code analyser that will automatically detect blocks of statements that violate the **DRY (Don't repeat yourself)** programming principle. The tool will identify groups of statements that have the same syntaxual 'signature' and attempt to refactor them into a new method definition, replacing the DRY violating statements with a new method call.

This project is currently a **WIP** and I strongly recommend to **avoid using the tool in production** until a more stable version is released.

# Features
* Identifies likely const values and only introduces variables where necessary
* Analyses data flow in and out of the statement blocks
* Created out/ref/in parameters where necessary
* Automatically creates delegates matching method calls
* Automatically return method call in sceneraios where value is directly returned
* Capable of compound refactoring jobs (multiple refactor instances in one fix)

# Examples

**Refactor example with out param:**

![Example1](https://i.imgur.com/soJGeWM.gif)

**Refactor example with delegate param:**

![Example2](https://i.imgur.com/WYcITIG.gif)

# Bugs / Known Issues
* Inconsecutive statements produce incorrect data flow analysis in some cases.
* Refactored return statements inside conditional blocks can lead to invalid return calls.
* Default parameters are included in auto generated delegatesd, invalidating method calls which utilise them.
* Explicit const values are still detected as paramaters
* In rare cases, parameter modifier and name are concatenated unintentionally


