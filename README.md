![Datasilk Logo](http://www.markentingh.com/projects/datasilk/logo.png)

# Scaffold
#### A light-weight templating engine built in C#, used for mutating strings such as HTML

* Inject dynamic data & lists
* Inherit templates inside other templates
* Use for MVC & MVVM projects

## Installation
Include this Github repository as a submodule within your ASP.NET Core project.

```
git submodule add http://github.com/Datasilk/Scaffold
```

## Usage with HTML
Load an html file to mutate with Scaffold.

```
<html>
    <head></head>
    <body>
        {{content}}
    </body>
    <footer>
        {{foot}}
    </footer>
</html>
{{scripts}}
```

Now, you can use Scaffold to replace the variables within your html file with dynamic data.

```
var scaffold = new Scaffold("/Views/Partial/layout.html")
scaffold.Data["content"] = "Hello World!";
return scaffold.Render();
```

