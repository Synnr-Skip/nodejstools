﻿//*********************************************************//
//    Copyright (c) Microsoft. All rights reserved.
//    
//    Apache 2.0 License
//    
//    You may obtain a copy of the License at
//    http://www.apache.org/licenses/LICENSE-2.0
//    
//    Unless required by applicable law or agreed to in writing, software 
//    distributed under the License is distributed on an "AS IS" BASIS, 
//    WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or 
//    implied. See the License for the specific language governing 
//    permissions and limitations under the License.
//
//*********************************************************//

using EnvDTE;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace TestUtilities {
    public static class TestExtensions {
        public static void SetStartupFile(this Project project, string name) {
            Assert.IsNotNull(project, "null project");
            Assert.IsNotNull(project.Properties, "null project properties " + project.Name + " " + project.GetType().FullName + " " + project.Kind);
            Assert.IsNotNull(project.Properties.Item("StartupFile"), "null startup file property" + project.Name + " " + project.GetType().FullName);
            project.Properties.Item("StartupFile").Value = name;
        }

        public static IEnumerable<int> FindIndexesOf(this string s, string substring) {
            int pos = 0;
            while (true) {
                pos = s.IndexOf(substring, pos);
                if (pos < 0) {
                    break;
                }
                yield return pos;
                pos++;
            }
        }

        public static HashSet<T> ToSet<T>(this IEnumerable<T> enumeration) {
            return new HashSet<T>(enumeration);
        }

        public static HashSet<T> ToSet<T>(this IEnumerable<T> enumeration, IEqualityComparer<T> comparer) {
            return new HashSet<T>(enumeration, comparer);
        }

        public static bool ContainsExactly<T>(this HashSet<T> set, IEnumerable<T> values) {
            if (set.Count != values.Count()) {
                return false;
            }
            foreach (var value in values) {
                if (!set.Contains(value)) {
                    return false;
                }
            }
            foreach (var value in set) {
                if (!values.Contains(value, set.Comparer)) {
                    return false;
                }
            }
            return true;
        }

        public static XName GetName(this XDocument doc, string localName) {
            return doc.Root.Name.Namespace.GetName(localName);
        }

        public static XElement Descendant(this XDocument doc, string localName) {
            return doc.Descendants(doc.Root.Name.Namespace.GetName(localName)).Single();
        }

        public static XElement Descendant(this XElement node, string localName) {
            return node.Descendants(node.Document.Root.Name.Namespace.GetName(localName)).Single();
        }
    }

}
