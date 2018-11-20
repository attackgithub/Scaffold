using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.ComponentModel;
using System.Runtime.Serialization.Formatters.Binary;
using Newtonsoft.Json;

[Serializable]
public struct SerializedScaffold
{
    public Dictionary<string, string> Data;
    public Dictionary<string, string> arguments;
    public List<ScaffoldElement> elements;
}

[Serializable]
public struct ScaffoldElement
{
    public string name;
    public string path;
    public string htm;
}

public class ScaffoldChild
{
    private Scaffold _parent;
    private string _id;
    public ScaffoldDictionary Data;

    public ScaffoldChild(Scaffold parent, string id)
    {
        _parent = parent;
        _id = id;
        Data = new ScaffoldDictionary(parent, id);
    }
}

public class ScaffoldDictionary : Dictionary<string, string>
{
    private Scaffold _parent;
    private string _id;
    public ScaffoldDictionary(Scaffold parent, string id)
    {
        _parent = parent;
        _id = id;
    }

#pragma warning disable CS0108 // Member hides inherited member; missing new keyword
    public string this[string key]
#pragma warning restore CS0108 // Member hides inherited member; missing new keyword
    {
        get
        {
            return _parent.Data[_id + "-" + key];
        }
        set
        {
            _parent.Data[_id + "-" + key] = value;
        }
    }
}

public class Scaffold
{
    public Dictionary<string, string> Data;
    public List<ScaffoldElement> elements;
    public string serializedElements;
    public string HTML = "";
    public string sectionName = "";
    public ScaffoldChild _child = null;

    public ScaffoldChild Child(string id) 
    {
        return new ScaffoldChild(this, id);
    }

    /// <summary>
    /// Use a template file to bind data and replace mustache variables with data, e.g. {{my-name}} is replaced with value of Scaffold.Data["my-name"]
    /// </summary>
    /// <param name="file">relative path to the template file</param>
    /// <param name="cache">Dictionary object used to save cached, parsed template to</param>
    public Scaffold(string file, Dictionary<String, SerializedScaffold> cache = null)
    {
        Setup(file, "", cache);
    }

    /// <summary>
    /// Use a template file to bind data and replace mustache variables with data, e.g. {{my-name}} is replaced with value of Scaffold.Data["my-name"]
    /// </summary>
    /// <param name="file">relative path to the template file</param>
    /// <param name="section">section name within the template file to load, e.g. {{my-section}} ... {{/my-section}}</param>
    /// <param name="cache">Dictionary object used to save cached, parsed template to</param>
    public Scaffold(string file, string section, Dictionary<String, SerializedScaffold> cache = null)
    {
        Setup(file, section, cache);
    }

    /// <summary>
    /// Binds an object to the scaffold template. Use e.g. {{myprop}} or {{myobj.myprop}} to represent object fields & properties in template
    /// </summary>
    /// <param name="obj"></param>
    public void Bind(object obj, string root = "")
    {
        if (obj != null)
        {
            foreach (PropertyDescriptor property in TypeDescriptor.GetProperties(obj))
            {
                object val = property.GetValue(obj);
                var name = (root != "" ? root + "." : "") + property.Name.ToLower();
                if (val == null)
                {
                    //do nothing
                }
                else if (val is string || val is int || val is long || val is double || val is decimal || val is short)
                {
                    //add property value to dictionary
                    Data[name] = val.ToString();
                }
                else if (val is bool)
                {
                    Data[name] = (bool)val == true ? "1" : "0";
                }
                else if (val is DateTime)
                {
                    Data[name] = ((DateTime)val).ToShortDateString() + " " + ((DateTime)val).ToShortTimeString();
                }
                else if (val is object)
                {
                    //recurse child object for properties
                    Bind(val, name);
                }
            }
        }
    }

    private void Setup(string file, string section = "", Dictionary<String, SerializedScaffold> cache = null, bool loadPartials = true)
    {
        SerializedScaffold cached = new SerializedScaffold() { elements = new List<ScaffoldElement>() };
        Data = new Dictionary<string, string>();
        sectionName = section;
        if (cache != null)
        {
            if (cache.ContainsKey(file + '/' + section) == true)
            {
                cached = cache[file + '/' + section];
                Data = cached.Data;
                elements = cached.elements;
            }
        }
        if (cached.elements.Count == 0)
        {
            elements = new List<ScaffoldElement>();

            //try loading file from disk
            if (File.Exists(MapPath(file)))
            {
                HTML = File.ReadAllText(MapPath(file));
            }
            if (HTML.Trim() == "") { return; }

            //next, find the group of code matching the scaffold section name
            if (section != "")
            {
                //find starting tag (optionally with arguments)
                //for example: {{button (name:submit, style:outline)}}
                int[] e = new int[3];
                e[0] = HTML.IndexOf("{{" + section);
                if (e[0] >= 0)
                {
                    e[1] = HTML.IndexOf("}", e[0]);
                    if (e[1] - e[0] <= 256)
                    {
                        e[1] = HTML.IndexOf("{{/" + section + "}}", e[1]);
                    }
                    else { e[0] = -1; }

                }

                if (e[0] >= 0 & e[1] > (e[0] + section.Length + 4))
                {
                    e[2] = e[0] + 4 + section.Length;
                    HTML = HTML.Substring(e[2], e[1] - e[2]);
                }
            }

            //get scaffold from html code
            var dirty = true;
            while (dirty == true)
            {
                dirty = false;
                var arr = HTML.Split("{{");
                var i = 0;
                var u = 0;
                var u2 = 0;
                ScaffoldElement scaff;

                //types of scaffold elements

                // {{title}}                        = variable
                // {{address}} {{/address}}         = block
                // {{button "/ui/button-medium"}}   = HTML include
                // {{button "/ui/button" title:"save", onclick="do.this()"}} = HTML include with variables

                //first, load all HTML includes
                for (var x = 0; x < arr.Length; x++)
                {
                    if (x == 0 && HTML.IndexOf(arr[x]) == 0)
                    {
                        arr[x] = "{!}" + arr[x];
                    }
                    else if (arr[x].Trim() != "")
                    {
                        i = arr[x].IndexOf("}}");
                        u = arr[x].IndexOf('"');
                        if (i > 0 && u > 0 && u < i - 2 && loadPartials == true)
                        {
                            //read partial include & load HTML from another file
                            scaff.name = arr[x].Substring(0, u - 1).Trim();
                            u2 = arr[x].IndexOf('"', u + 2);

                            //load the scaffold HTML
                            var newScaff = new Scaffold(arr[x].Substring(u + 1, u2 - u - 1), "", cache);

                            //check for HTML include variables
                            if (i - u2 > 0)
                            {
                                var vars = arr[x].Substring(u2 + 1, i - (u2 + 1)).Trim();
                                if (vars.IndexOf(":") > 0)
                                {
                                    //HTML include variables exist
                                    var kv = (Dictionary<string, string>)JsonConvert.DeserializeObject("{" + vars + "}", typeof(Dictionary<string, string>));
                                    foreach(var kvp in kv)
                                    {
                                        newScaff.Data[kvp.Key] = kvp.Value;
                                    }
                                }
                            }

                            //rename child scaffold variables with a prefix of "scaff.name-"
                            var ht = newScaff.Render(newScaff.Data, false);
                            var y = 0;
                            var prefix = scaff.name + "-";
                            while (y >= 0)
                            {
                                y = ht.IndexOf("{{", y);
                                if (y < 0) { break; }
                                if (ht.Substring(y + 2, 1) == "/")
                                {
                                    ht = ht.Substring(0, y + 3) + prefix + ht.Substring(y + 3);
                                }
                                else
                                {
                                    ht = ht.Substring(0, y + 2) + prefix + ht.Substring(y + 2);
                                }
                                y += 2;
                            }
                            arr[x] = "{!}" + ht + arr[x].Substring(i + 2);
                            HTML = JoinHTML(arr);
                            dirty = true; //HTML is dirty, restart loop
                            break;
                        }
                    }

                }
                if (dirty == false)
                {
                    //next, process variables & blocks
                    for (var x = 0; x < arr.Length; x++)
                    {
                        if (x == 0 && HTML.IndexOf(arr[0].Substring(3)) == 0)//skip "{!}" using substring
                        {
                            //first element is HTML only
                            elements.Add(new ScaffoldElement() { htm = arr[x].Substring(3), name = "" });
                        }
                        else if (arr[x].Trim() != "")
                        {
                            i = arr[x].IndexOf("}}");
                            u = arr[x].IndexOf('"');
                            scaff = new ScaffoldElement();
                            if (i > 0)
                            {
                                scaff.htm = arr[x].Substring(i + 2);
                                scaff.name = arr[x].Substring(0, i).Trim();

                                //get optional path stored within variable tag (if exists)
                                //e.g. {{my-component "list"}}
                                if(u > 0 && u < i - 2)
                                {
                                    u2 = arr[x].IndexOf('"', u + 2);
                                    if (i - u2 > 0)
                                    {
                                        var data = arr[x].Substring(u + 1, u2 - u - 1);
                                        scaff.path = data;
                                    }
                                }
                            }
                            else
                            {
                                scaff.name = "";
                                scaff.htm = arr[x];
                            }
                            elements.Add(scaff);
                        }
                    }
                }
            }
            //cache the scaffold data
            if (cache != null)
            {
                var scaffold = new SerializedScaffold
                {
                    Data = Data,
                    elements = elements
                };
                cache.Add(file + '/' + section, scaffold);
            }
        }
    }

    private string JoinHTML(string[] html)
    {
        for (var x = 0; x < html.Length; x++)
        {
            switch (html[x].Substring(0, 3))
            {
                case "{!}":
                    html[x] = html[x].Substring(3);
                    break;
                default:
                    html[x] = "{{" + html[x];
                    break;
            }
        }


        return string.Join("", html);
    }

    public string Render()
    {
        return Render(Data);
    }

    public string Render(Dictionary<string, string> nData, bool hideElements = true)
    {
        //deserialize list of elements since we will be manipulating the list,
        //so we don't want to permanently mutate the public elements array
        var elems = (List<ScaffoldElement>)DeepCopy<List<ScaffoldElement>>(elements);
        if (elems.Count > 0)
        {
            //render scaffold with paired nData data
            var scaff = new StringBuilder();
            var s = "";
            var useScaffold = false;
            var closing = new List<List<string>>();

            //remove any unwanted blocks of HTML from scaffold
            for (var x = 0; x < elems.Count; x++)
            {
                if (x < elems.Count - 1)
                {
                    for (var y = x + 1; y < elems.Count; y++)
                    {
                        //check for closing tag
                        if (elems[y].name == "/" + elems[x].name)
                        {
                            //add enclosed group of HTML to list for removing
                            List<string> closed = new List<string>
                            {
                                elems[x].name,
                                x.ToString(),
                                y.ToString()
                            };

                            if (nData.ContainsKey(elems[x].name) == true)
                            {
                                //check if user wants to include HTML 
                                //that is between start & closing tag   
                                s = nData[elems[x].name];
                                if (string.IsNullOrEmpty(s) == true) { s = ""; }
                                if (s == "true" | s == "1")
                                {
                                    closed.Add("true");
                                }
                                else { closed.Add(""); }
                            }
                            else { closed.Add(""); }

                            closing.Add(closed);
                        }
                    }

                }
            }

            if(hideElements == true)
            {
                //remove all groups of HTML in list that should not be displayed
                List<int> removeIndexes = new List<int>();
                bool isInList = false;
                for (int x = 0; x < closing.Count; x++)
                {
                    if (closing[x][3] != "true")
                    {
                        //add range of indexes from closing to the removeIndexes list
                        for (int y = int.Parse(closing[x][1]); y < int.Parse(closing[x][2]); y++)
                        {
                            isInList = false;
                            for (int z = 0; z < removeIndexes.Count; z++)
                            {
                                if (removeIndexes[z] == y) { isInList = true; break; }
                            }
                            if (isInList == false) { removeIndexes.Add(y); }
                        }
                    }
                }

                //physically remove HTML list items from scaffold
                int offset = 0;
                for (int z = 0; z < removeIndexes.Count; z++)
                {
                    elems.RemoveAt(removeIndexes[z] - offset);
                    offset += 1;
                }
            }

            //finally, replace scaffold variables with custom data
            for (var x = 0; x < elems.Count; x++)
            {
                //check if scaffold item is an enclosing tag or just a variable
                useScaffold = true;
                if (elems[x].name.IndexOf('/') < 0)
                {
                    for (int y = 0; y < closing.Count; y++)
                    {
                        if (elems[x].name == closing[y][0]) { useScaffold = false; break; }
                    }
                }
                else { useScaffold = false; }

                if ((nData.ContainsKey(elems[x].name) == true
                || elems[x].name.IndexOf('/') == 0) & useScaffold == true)
                {
                    //inject string into scaffold variable
                    s = nData[elems[x].name.Replace("/", "")];
                    if (string.IsNullOrEmpty(s) == true) { s = ""; }
                    scaff.Append(s + elems[x].htm);
                }
                else
                {
                    //passively add htm, ignoring scaffold variable
                    scaff.Append((hideElements == true ? "" : (elems[x].name != "" ? "{{" + elems[x].name + "}}" : "")) + elems[x].htm);
                }
            }

            //render scaffolding as HTML string
            return scaff.ToString();
        }
        return "";
    }

    public string Get(string name)
    {
        var index = elements.FindIndex(c => c.name == name);
        if (index < 0) { return ""; }
        var part = elements[index];
        var html = part.htm;
        for (var x = index + 1; x < elements.Count; x++)
        {
            part = elements[x];
            if (part.name == "/" + name) { break; }

            //add inner scaffold elements
            if (part.name.IndexOf('/') < 0)
            {
                if (Data.ContainsKey(part.name))
                {
                    if (Data[part.name] == "1" || Data[part.name].ToLower() == "true")
                    {
                        html += Get(part.name);
                    }
                }
            }
            else
            {
                html += part.htm;
            }

        }

        return html;
    }

    private static T DeepCopy<T>(T obj)

    {
        if (!typeof(T).IsSerializable)
        {
            throw new Exception("The source object must be serializable");
        }

        if (Object.ReferenceEquals(obj, null))
        {
            throw new Exception("The source object must not be null");
        }

        T result = default(T);
        using (var memoryStream = new MemoryStream())
        {
            var formatter = new BinaryFormatter();
            formatter.Serialize(memoryStream, obj);
            memoryStream.Seek(0, SeekOrigin.Begin);
            result = (T)formatter.Deserialize(memoryStream);
            memoryStream.Close();
        }
        return result;

    }

    private static string MapPath(string strPath = "")
    {
        var path = Path.GetFullPath(".") + "\\";
        var str = strPath.Replace("/", "\\");
        if (str.Substring(0, 1) == "\\") { str = str.Substring(1); }
        return path + str;
    }
}
