![Datasilk Logo](http://www.markentingh.com/projects/datasilk/logo.png)

# Scaffold
#### A light-weight templating engine built in C#, used for mutating documents such as HTML

* Inject dynamic data & lists
* Import partial templates
* Use for MVC & MVVM projects

## Installation
Include this Github repository as a submodule within your ASP.NET Core project.

```
git submodule add http://github.com/Datasilk/Scaffold
```

## Usage with HTML
Load an html file to mutate with `Scaffold`.

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

Now, you can use `Scaffold` to replace the variables within your html file with dynamic data.

```
var scaffold = new Scaffold("/Views/Shared/layout.html")
scaffold.Data["content"] = "Hello World!";
return scaffold.Render();
```

### Import partial Templates

You can also import partial templates from within a template

Source for `Views/home.html`
```
<html>
	<head></head>
	<body>
		{{header "Views/Shared/header.html"}}
		{{content}}
	</body>
</html>
```

Source for partial view `Views/Shared/header.html`

```
<header>
	<img src="/images/logo.png"/>
	<span class="company">{{company}}</span>
	<div class="user">Welcome, {{username}}</div>
</header>
```

Now, you can use `Scaffold` to replace variables that reside in the partial template with dynamic data.

```
var scaffold = new Scaffold("/Views/home.html")
var header = scaffold.Child("header");
header.Data["company"] = "Datasilk";
header.Data["username"] = User.name;
return scaffold.Render();
```


### Predefined variables
When importing a partial template, you can define default values for the variables that reside within the partial template.

Source for `Views/home.html`
```
<html>
	<head></head>
	<body>
		{{header "Views/Shared/header.html" company:"Datasilk", username:"please log in"}}
		{{content}}
	</body>
</html>
```

In the above example, the default value for the variable `username` that exists in `Views/Shared/header.html` will instruct the user to log into their account. 

```
var scaffold = new Scaffold("/Views/home.html")
if(User.id > 0)
{
	scaffold.Child("header").Data["username"] = User.name;
}
return scaffold.Render();
```

`Scaffold` can replace the default value with the user's name if the user has logged into their account.

