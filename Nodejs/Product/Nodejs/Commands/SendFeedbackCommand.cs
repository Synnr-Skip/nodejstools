// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using System;
using System.Diagnostics;
using Microsoft.VisualStudioTools;

namespace Microsoft.NodejsTools.Commands
{
    internal sealed class SendFeedbackCommand : Command
    {
        public override void DoCommand(object sender, EventArgs args)
        {
            Process.Start(@"https://aka.ms/ntvs-feedback");
        }

        public override int CommandId => (int)PkgCmdId.cmdidSendFeedback;
    }
}

