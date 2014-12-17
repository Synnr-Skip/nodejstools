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

using System;
using System.Runtime.Serialization;

namespace Microsoft.NodejsTools.Debugger.Commands {
    [Serializable]
    class DebuggerCommandException : Exception {
        public DebuggerCommandException() { }
        public DebuggerCommandException(string message) : base(message) { }
        public DebuggerCommandException(string message, Exception innerException) : base(message, innerException) { }
        protected DebuggerCommandException(SerializationInfo info, StreamingContext context) : base(info, context) { }
    }
}