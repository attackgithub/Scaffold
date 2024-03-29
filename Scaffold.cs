using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Text;
using System.ComponentModel;
using System.Runtime.Serialization.Formatters.Binary;
using Newtonsoft.Json;

[Serializable]
public struct SerializedScaffold
{
    public ScaffoldData Data;
    public Dictionary<string, int[]> Fields;
    public List<ScaffoldElement> Elements;
    public List<ScaffoldPartial> Partials;
}

[Serializable]
public struct ScaffoldElement
{
    public string Name;
    public string Path;
    public string Htm;
    public Dictionary<string, string> Vars;
}

public static class ScaffoldCache
{
    public static Dictionary<string, SerializedScaffold> cache { get; set; }
}

public class ScaffoldChild
{
    private ScaffoldDictionary data;
    public Dictionary<string, int[]> Fields = new Dictionary<string, int[]>();

    public ScaffoldChild(Scaffold parent, string id)
    {
        data = new ScaffoldDictionary(parent, id);
        //load related fields
        foreach (var item in parent.Fields)
        {
            if (item.Key.IndexOf(id + "-") == 0)
            {
                Fields.Add(item.Key.Replace(id + "-", ""), item.Value);
            }
        }
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
                    data[name] = "";
                }
                else if (val is string || val is int || val is long || val is double || val is decimal || val is short)
                {
                    //add property value to dictionary
                    data[name] = val.ToString();
                }
                else if (val is bool)
                {
                    data[name] = (bool)val == true ? "1" : "0";
                }
                else if (val is DateTime)
                {
                    data[name] = ((DateTime)val).ToShortDateString() + " " + ((DateTime)val).ToShortTimeString();
                }
                else if (val is object)
                {
                    //recurse child object for properties
                    Bind(val, name);
                }
            }
        }
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
            return _parent[_id + "-" + key];
        }
        set
        {
            _parent[_id + "-" + key] = value;
        }
    }
}

[Serializable]
public class ScaffoldPartial
{
    public string Name { get; set; }
    public string Path { get; set; }
    public string Prefix { get; set; } //prefix used in html variable names after importing the partial
}

public class ScaffoldData : IDictionary<string, string>
{
    private Dictionary<string, string> _data = new Dictionary<string, string>();

    public string this[string key] {
        get
        {
            return _data[key];
        }
        set
        {
            _data[key] = value;
        }
    }

    public bool this[string key, bool isBool]
    {
        get
        {
            if (_data[key] == "True")
            {
                return true;
            }
            return false;
        }

        set
        {
            if (value)
            {
                _data[key] = "True";
            }
            else
            {
                _data[key] = "False";
            }
        }
    }

    public ICollection<string> Keys => _data.Keys;

    public ICollection<string> Values => _data.Values;

    public int Count => _data.Count;

    public bool IsReadOnly => false;

    public void Add(string key, string value)
    {
        _data.Add(key, value);
    }

    public void Add(string key, bool value)
    {
        _data.Add(key, value.ToString());
    }

    public void Add(KeyValuePair<string, string> item)
    {
        _data.Add(item.Key, item.Value);
    }

    public void Clear()
    {
        _data.Clear();
    }

    public bool Contains(KeyValuePair<string, string> item)
    {
        return _data.Contains(item);
    }

    public bool ContainsKey(string key)
    {
        return _data.ContainsKey(key);
    }

    public void CopyTo(KeyValuePair<string, string>[] array, int arrayIndex)
    {
        throw new NotImplementedException();
    }

    public IEnumerator<KeyValuePair<string, string>> GetEnumerator()
    {
        return _data.GetEnumerator();
    }

    public bool Remove(string key)
    {
        return _data.Remove(key);
    }

    public bool Remove(KeyValuePair<string, string> item)
    {
        if (_data.Contains(item))
        {
            return _data.Remove(item.Key);
        }
        return false;
    }

    public bool TryGetValue(string key, out string value)
    {
        return _data.TryGetValue(key, out value);
    }

    IEnumerator IEnumerable.GetEnumerator()
    {
        return _data.GetEnumerator();
    }
}

public class Scaffold
{
    public List<ScaffoldElement> Elements;
    public List<ScaffoldPartial> Partials = new List<ScaffoldPartial>();
    public Dictionary<string, int[]> Fields = new Dictionary<string, int[]>();
    public string HTML = "";
    public string Section = ""; //section of the template to use for rendering

    private ScaffoldData data;
    private Dictionary<string, ScaffoldChild> children = null;

    public ScaffoldChild Child(string id)
    {
        if (children == null)
        {
            children = new Dictionary<string, ScaffoldChild>();
        }
        if (!children.ContainsKey(id))
        {
            children.Add(id, new ScaffoldChild(this, id));
        }
        return children[id];
    }

    /// <summary>
    /// Use a template file to bind data and replace mustache variables with data, e.g. {{my-name}} is replaced with value of Scaffold["my-name"]
    /// </summary>
    /// <param name="file">relative path to the template file</param>
    /// <param name="cache">Dictionary object used to save cached, parsed template to</param>
    public Scaffold(string file, Dictionary<string, SerializedScaffold> cache = null)
    {
        Setup(file, "", cache);
    }

    /// <summary>
    /// Use a template file to bind data and replace mustache variables with data, e.g. {{my-name}} is replaced with value of Scaffold["my-name"]
    /// </summary>
    /// <param name="file">relative path to the template file</param>
    /// <param name="section">section name within the template file to load, e.g. {{my-section}} ... {{/my-section}}</param>
    /// <param name="cache">Dictionary object used to save cached, parsed template to</param>
    public Scaffold(string file, string section, Dictionary<string, SerializedScaffold> cache = null)
    {
        Setup(file, section, cache);
    }

    public string this[string key]
    {
        get
        {
            return data[key];
        }
        set
        {
            data[key] = value;
        }
    }

    public void Show(string blockKey)
    {
        data[blockKey, true] = true;
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
                    data[name] = "";
                }
                else if (val is string || val is int || val is long || val is double || val is decimal || val is short)
                {
                    //add property value to dictionary
                    data[name] = val.ToString();
                }
                else if (val is bool)
                {
                    data[name] = (bool)val == true ? "1" : "0";
                }
                else if (val is DateTime)
                {
                    data[name] = ((DateTime)val).ToShortDateString() + " " + ((DateTime)val).ToShortTimeString();
                }
                else if (val is object)
                {
                    //recurse child object for properties
                    Bind(val, name);
                }
            }
        }
    }

    private void Setup(string file, string section = "", Dictionary<string, SerializedScaffold> cache = null, bool loadPartials = true)
    {
        SerializedScaffold cached = new SerializedScaffold() { Elements = new List<ScaffoldElement>() };
        data = new ScaffoldData();
        Section = section;
        if (cache == null && ScaffoldCache.cache != null)
        {
            cache = ScaffoldCache.cache;
        }
        if (cache != null)
        {
            if (cache.ContainsKey(file + '/' + section) == true)
            {
                cached = cache[file + '/' + section];
                data = cached.Data;
                Elements = cached.Elements;
                Fields = cached.Fields;
            }
        }
        if (cached.Elements.Count == 0)
        {
            Elements = new List<ScaffoldElement>();

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
                var s = 0;
                var c = 0;
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
                        s = arr[x].IndexOf(':');
                        u = arr[x].IndexOf('"');
                        if (i > 0 && u > 0 && u < i - 2 && (s == -1 || s > u) && loadPartials == true)
                        {
                            //read partial include & load HTML from another file
                            scaff.Name = arr[x].Substring(0, u - 1).Trim();
                            u2 = arr[x].IndexOf('"', u + 2);
                            var partial_path = arr[x].Substring(u + 1, u2 - u - 1);

                            //load the scaffold HTML
                            var newScaff = new Scaffold(partial_path, "", cache);

                            //check for HTML include variables
                            if (i - u2 > 0)
                            {
                                var vars = arr[x].Substring(u2 + 1, i - (u2 + 1)).Trim();
                                if (vars.IndexOf(":") > 0)
                                {
                                    //HTML include variables exist
                                    try
                                    {
                                        var kv = (Dictionary<string, string>)JsonConvert.DeserializeObject("{" + vars + "}", typeof(Dictionary<string, string>));
                                        foreach (var kvp in kv)
                                        {
                                            newScaff[kvp.Key] = kvp.Value;
                                        }
                                    }
                                    catch (Exception)
                                    {
                                    }
                                }
                            }

                            //rename child scaffold variables with a prefix of "scaff.name-"
                            var ht = newScaff.Render(newScaff.data, false);
                            var y = 0;
                            var prefix = scaff.Name + "-";
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

                            Partials.Add(new ScaffoldPartial() { Name = scaff.Name, Path = partial_path, Prefix = prefix });
                            Partials.AddRange(newScaff.Partials.Select(a =>
                            {
                                var partial = a;
                                partial.Prefix = prefix + partial.Prefix;
                                return partial;
                            })
                            );
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
                            Elements.Add(new ScaffoldElement() { Htm = arr[x].Substring(3), Name = "" });
                        }
                        else if (arr[x].Trim() != "")
                        {
                            i = arr[x].IndexOf("}}");
                            s = arr[x].IndexOf(' ');
                            c = arr[x].IndexOf(':');
                            u = arr[x].IndexOf('"');
                            scaff = new ScaffoldElement();
                            if (i > 0)
                            {
                                scaff.Htm = arr[x].Substring(i + 2);

                                //get variable name
                                if (s < i && s > 0)
                                {
                                    //found space
                                    scaff.Name = arr[x].Substring(0, s).Trim();
                                }
                                else
                                {
                                    //found tag end
                                    scaff.Name = arr[x].Substring(0, i).Trim();
                                }

                                if (scaff.Name.IndexOf('/') < 0)
                                {
                                    if (Fields.ContainsKey(scaff.Name))
                                    {
                                        //add element index to existing field
                                        var field = Fields[scaff.Name];
                                        Fields[scaff.Name] = field.Append(Elements.Count).ToArray();
                                    }
                                    else
                                    {
                                        //add field with element index
                                        Fields.Add(scaff.Name, new int[] { Elements.Count });
                                    }
                                }

                                //get optional path stored within variable tag (if exists)
                                //e.g. {{my-component "list"}}
                                if (u > 0 && u < i - 2 && (c == -1 || c > u))
                                {
                                    u2 = arr[x].IndexOf('"', u + 2);
                                    if (i - u2 > 0)
                                    {
                                        var data = arr[x].Substring(u + 1, u2 - u - 1);
                                        scaff.Path = data;
                                    }
                                }
                                else if (s < i && s > 0)
                                {
                                    //get optional variables stored within tag
                                    var vars = arr[x].Substring(s + 1, i - s - 1);
                                    try
                                    {
                                        scaff.Vars = (Dictionary<string, string>)JsonConvert.DeserializeObject("{" + vars + "}", typeof(Dictionary<string, string>));
                                    }
                                    catch (Exception)
                                    {
                                    }

                                }
                            }
                            else
                            {
                                scaff.Name = "";
                                scaff.Htm = arr[x];
                            }
                            Elements.Add(scaff);
                        }
                    }
                }
            }
            //cache the scaffold data
            if (cache != null)
            {
                var scaffold = new SerializedScaffold
                {
                    Data = data,
                    Elements = Elements,
                    Fields = Fields,
                    Partials = Partials
                };
                cache.Add(file + '/' + section, scaffold);
            }
        }
    }

    private string JoinHTML(string[] html)
    {
        for (var x = 0; x < html.Length; x++)
        {
            if (html[x].Substring(0, 3) == "{!}")
            {
                html[x] = html[x].Substring(3);
            }
            else
            {
                html[x] = "{{" + html[x];
            }
        }
        return string.Join("", html);
    }

    public string Render()
    {
        return Render(data);
    }

    private class ClosingElement
    {
        public string Name;
        public int Start;
        public int End;
        public List<bool> Show { get; set; } = new List<bool>();
    }

    public string Render(ScaffoldData nData, bool hideElements = true)
    {
        //deserialize list of elements since we will be manipulating the list,
        //so we don't want to permanently mutate the public elements array
        var elems = DeepCopy(Elements);
        if (elems.Count > 0)
        {
            //render scaffold with paired nData data
            var scaff = new StringBuilder();

            var closing = new List<ClosingElement>();
            //remove any unwanted blocks of HTML from scaffold
            for (var x = 0; x < elems.Count; x++)
            {
                if (x < elems.Count - 1)
                {
                    for (var y = x + 1; y < elems.Count; y++)
                    {
                        //check for closing tag
                        if (elems[y].Name == "/" + elems[x].Name)
                        {
                            //add enclosed group of HTML to list for removing
                            var closed = new ClosingElement()
                            {
                                Name = elems[x].Name,
                                Start = x,
                                End = y
                            };

                            if (nData.ContainsKey(elems[x].Name) == true)
                            {
                                //check if user wants to include HTML 
                                //that is between start & closing tag  
                                if (nData[elems[x].Name, true] == true)
                                {
                                    closed.Show.Add(true);
                                }
                                else
                                {
                                    closed.Show.Add(false);
                                }
                            }
                            else {
                                closed.Show.Add(false);
                            }

                            closing.Add(closed);
                            break;
                        }
                    }

                }
            }

            if (hideElements == true)
            {
                //remove all groups of HTML in list that should not be displayed
                List<int> removeIndexes = new List<int>();
                bool isInList = false;
                for (int x = 0; x < closing.Count; x++)
                {
                    if (closing[x].Show.FirstOrDefault() != true)
                    {
                        //add range of indexes from closing to the removeIndexes list
                        for (int y = closing[x].Start; y < closing[x].End; y++)
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
                var useScaffold = true;
                if (elems[x].Name.IndexOf('/') < 0)
                {
                    for (int y = 0; y < closing.Count; y++)
                    {
                        if (elems[x].Name == closing[y].Name)
                        {
                            useScaffold = false; break;
                        }
                    }
                }
                else { useScaffold = false; }

                if ((nData.ContainsKey(elems[x].Name) == true
                || elems[x].Name.IndexOf('/') == 0) & useScaffold == true)
                {
                    //inject string into scaffold variable
                    var s = nData[elems[x].Name.Replace("/", "")];
                    if (string.IsNullOrEmpty(s) == true) { s = ""; }
                    scaff.Append(s + elems[x].Htm);
                }
                else
                {
                    //passively add htm, ignoring scaffold variable
                    scaff.Append((hideElements == true ? "" : (elems[x].Name != "" ? "{{" + elems[x].Name + "}}" : "")) + elems[x].Htm);
                }
            }

            //render scaffolding as HTML string
            return scaff.ToString();
        }
        return "";
    }

    public string Get(string name)
    {
        var index = Elements.FindIndex(c => c.Name == name);
        if (index < 0) { return ""; }
        var part = Elements[index];
        var html = part.Htm;
        for (var x = index + 1; x < Elements.Count; x++)
        {
            part = Elements[x];
            if (part.Name == "/" + name) { break; }

            //add inner scaffold elements
            if (part.Name.IndexOf('/') < 0)
            {
                if (data.ContainsKey(part.Name))
                {
                    if (data[part.Name, true] == true)
                    {
                        html += Get(part.Name);
                    }
                }
            }
            else
            {
                html += part.Htm;
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

        if (obj == null)
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
