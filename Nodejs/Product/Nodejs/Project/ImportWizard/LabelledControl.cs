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

using System.Windows;
using System.Windows.Controls;

namespace Microsoft.PythonTools.Project.ImportWizard {
    class LabelledControl : ContentControl {
        public string Title {
            get { return (string)GetValue(TitleProperty); }
            set { SetValue(TitleProperty, value); }
        }

        public static readonly DependencyProperty TitleProperty = DependencyProperty.Register("Title", typeof(string), typeof(LabelledControl), new PropertyMetadata());

        public string HelpText {
            get { return (string)GetValue(HelpTextProperty); }
            set { SetValue(HelpTextProperty, value); }
        }

        public static readonly DependencyProperty HelpTextProperty = DependencyProperty.Register("HelpText", typeof(string), typeof(LabelledControl), new PropertyMetadata(HelpText_PropertyChanged));

        private static void HelpText_PropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) {
            d.SetValue(HasHelpTextPropertyKey, !string.IsNullOrWhiteSpace(e.NewValue as string));
        }


        public bool HasHelpText {
            get { return (bool)GetValue(HasHelpTextProperty); }
            private set { SetValue(HasHelpTextPropertyKey, value); }
        }

        private static readonly DependencyPropertyKey HasHelpTextPropertyKey = DependencyProperty.RegisterReadOnly("HasHelpText", typeof(bool), typeof(LabelledControl), new PropertyMetadata(false));
        public static readonly DependencyProperty HasHelpTextProperty = HasHelpTextPropertyKey.DependencyProperty;

    }
}
