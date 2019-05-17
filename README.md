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
scaffold["content"] = "Hello World!";
return scaffold.Render();
```

### Show Blocks of HTML
You can render optional blocks of content when neccessary.
```
<h3>{{Title}}</h3>
{{has-author}}
	<span>Published by {{author}}</span>
{{/has-author}}
```

Now, you can use `Scaffold` to render the optional author information.

```
var scaffold = new Scaffold("/Views/Article/article.html")
scaffold["Title"] = "Hello World!";
if(author != ""){
	scaffold.Show("has-author");
	scaffold["author"] = author;
}
return scaffold.Render();
```


### Bind Objects to Templates
You can bind complex C# objects to templates

```
<div class="user">
	<span class="name">{{user.name}}</span>
	<img src="{{user.image}}"/>
</div>
```

Now, you can use `Scaffold` to bind variables within your html file to C# objects.

```
var scaffold = new Scaffold("/Views/UI/header.html")
scaffold.Bind(new {
	User = new {
		myUser.Name,
		Image = myUser.Photo == 1 ? "/images/users/" + myUser.Id + ".jpg" : "/images/nophoto.jpg"
	}
});
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
header["company"] = "Datasilk";
header["username"] = User.name;
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
if(User.Id > 0)
{
	scaffold.Child("header")["username"] = User.Name;
}
return scaffold.Render();
```

In the above example,`Scaffold` will replace the default value with the user's name if the user has logged into their account.


### Variable properties
Variables can contain custom properties that may determine what to render in place of the variable.

Source for `Views/users.html`
```
<html>
	<head></head>
	<body>
		{{user-list filter:"Mark", length:"10"}}
	</body>
</html>
```

In the above example, the app will generate a user list based on a search filter on the user name and will display a maximum of 10 results.

```
var scaffold = new Scaffold("/Views/users.html")
var index = scaffold.Fields["user-list"].First();
var elem = scaffold.Elements[index];
var filter = elem.Vars["filter"] ?? "";
var length = elem.Vars["length"] ?? 20;

// generate user list
var html = new StringBuilder();
//...load html users list into string builder, then...
scaffold["user-list"] = html.ToString();
return scaffold.Render();
```

In the above example,`Scaffold` will get properties for the *user-list* variable, then generate a user list based on the given *filter* & *length* properties

