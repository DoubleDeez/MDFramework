# Contributing to MDFramework

## Help Other Users
If you're familiar with the framework, you can help out others in the [discord server](https://discord.gg/UH49eHK).

## Reporting Bugs
If something isn't working quite as expected, report it on the [issue tracker](https://github.com/DoubleDeez/MDFramework/issues) and give it the `bug` label.
Make sure to accurate describe your situation, the behaviour you're seeing, and the behaviour you're expecting. Consider also attaching a minimal example project to make it easier for others to reproduce.

Sometimes there are typos or wrong/missing information in comments and documentation. Those instances should also be reported on the [issue tracker](https://github.com/DoubleDeez/MDFramework/issues) and be given the `documentation` label.

## Making Suggestions
If there's a feature you'd like to see added to the framework, head over to the [issue tracker](https://github.com/DoubleDeez/MDFramework/issues) and create a new issue.
In your issue, thoroughly explain the feature you would like to see and why it would be a good addition to the framework.
Then add the `question` label to the issue and a collaborator will take a look and decide if they think your idea is suitable for the framework.

## Submitting a Pull Request
Every pull request should have a corresponding [issue](https://github.com/DoubleDeez/MDFramework/issues) that it fixes.
If you're looking for something to fix for the first time, try taking a look at issues with the [good first issue](https://github.com/DoubleDeez/MDFramework/issues?q=is%3Aissue+is%3Aopen+label%3A%22good+first+issue%22) label.

### Forking and Feature Branches
To submit changes to MDFramework via a Pull Request, first you should fork the repo to have a copy of your own.
Pull requests that get accepted to MDFramework will be Squashed & Merged to keep the changes in a single commit.
To prevent that from messing up the history of your fork's master branch, you should create feature branches for each pull request you'll make.
This will keep your commits to the feature branch then once your pull request is accepted and merged, you can pull latest with the squashed commit to your master branch.

### Wiki Updates
Sometimes a pull request will require changes to the Wiki, but since only collaborators are allowed to modify the wiki, you might not be able to make the update yourself.
Instead, your pull request description should include what changes need to be made and the full write-up to be added to the wiki, if applicable.
Then a collaborator will make the wiki changes for you when the pull request is merged.

### Adding New Examples
If either your pull request adds a new feature that could benefit from an example or you want to add an example for a feature that doesn't have one yet, create a pull request for the [Examples Repo](https://github.com/DoubleDeez/MDFramework-Examples).

### Code Style Guidelines
#### Comments
MDFramework uses [XML Comments](https://docs.microsoft.com/en-us/dotnet/csharp/codedoc). At a minimum, every accessible class, member, type, etc should have a completed `<summary>` tag.
By accessible, it's meant that anything that could be referenced or used by an MDFramework user, not necessarily a contributor. This will typically mean, if it's marked `public` or `protected` then it should have a `<summary>`.

#### Indentation
MDFramework uses 4-space indents.

#### Nesting
Nesting should be minimized, if reversing logic can reduce the amount of nesting, please do so.

#### Letter Casing
PascalCase should be used globally. There are some exceptions, for example const variables should use SCREAMING_SNAKE_CASE.

#### Braces
The opening brace should always be on a new line unless if the block is empty then the opening and closing brace can be on the same line.
```cs
virtual int MyVirtualFunc()
{
    return -1;
}
```
```cs
virtual void MyVirtualFunc() {}
```

Single line blocks for an `if` or `for` should always have braces.
```cs
if (condition == false)
{
    return;
}
```

#### Regions
The use of regions is optional, but if the file you're modifying already has regions, please continue to adhere to them.

#### String Interpolation
String interpolation is prefered over format strings.
```cs
MDLog.Error(LOG_CAT, $"The value {value} was below 0");
```

#### Class Order
The main class for the file should be at the bottom of the file (eg. `MDGameInstance` in `MDGameInstance.cs`).
The order of appearance otherwise from top to bottom should be: `namespace`, `enum`, `struct`, `class`.
When inside of other blocks, these should appear before the fields, properties, and methods.

#### Class Member Order
Field and Properties should come before methods.

#### MD Namespace
All aspects of MDFramework should be in the `MD` namespace.