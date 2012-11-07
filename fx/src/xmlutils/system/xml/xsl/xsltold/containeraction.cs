//------------------------------------------------------------------------------
// <copyright file="ContainerAction.cs" company="Microsoft">
//     
//      Copyright (c) 2006 Microsoft Corporation.  All rights reserved.
//     
//      The use and distribution terms for this software are contained in the file
//      named license.txt, which can be found in the root of this distribution.
//      By using this software in any fashion, you are agreeing to be bound by the
//      terms of this license.
//     
//      You must not remove this notice, or any other, from this software.
//     
// </copyright>
//------------------------------------------------------------------------------

namespace System.Xml.Xsl.XsltOld {
    using Res = System.Xml.Utils.Res;
    using System;
    using System.Diagnostics;
    using System.Text;
    using System.Globalization;
    using System.Xml;
    using System.Xml.XPath;
    using System.Xml.Xsl.Runtime;
    using MS.Internal.Xml.XPath;
    using System.Collections;

    internal class NamespaceInfo {
        internal String prefix;
        internal String nameSpace;
        internal int stylesheetId;

        internal NamespaceInfo(String prefix, String nameSpace, int stylesheetId) {
            this.prefix = prefix;
            this.nameSpace = nameSpace;
            this.stylesheetId = stylesheetId;
        }
    }

    internal class ContainerAction : CompiledAction {
        internal ArrayList      containedActions;
        internal CopyCodeAction lastCopyCodeAction; // non null if last action is CopyCodeAction;

        private  int  maxid = 0;

        // Local execution states
        protected const int ProcessingChildren = 1;

        internal override void Compile(Compiler compiler) {
            throw new NotImplementedException();
        }

        internal void CompileStylesheetAttributes(Compiler compiler) {
            NavigatorInput input        = compiler.Input;
            string         element      = input.LocalName;
            string         badAttribute = null;
            string         version      = null;

            if (input.MoveToFirstAttribute()) {
                do {
                    string nspace = input.NamespaceURI;
                    string name   = input.LocalName;

                    if (! Keywords.Equals(nspace, input.Atoms.Empty)) continue;

                    if (Keywords.Equals(name, input.Atoms.Version)) {
                        version = input.Value;
                        if (1 <= XmlConvert.ToXPathDouble(version)) {
                            compiler.ForwardCompatibility = (version != Keywords.s_Version10);
                        }
                        else {
                            // XmlConvert.ToXPathDouble(version) an be NaN!
                            if (! compiler.ForwardCompatibility) {
                                throw XsltException.Create(Res.Xslt_InvalidAttrValue, Keywords.s_Version, version);
                            }
                        }
                    }
                    else if (Keywords.Equals(name, input.Atoms.ExtensionElementPrefixes)) {
                        compiler.InsertExtensionNamespace(input.Value);
                    }
                    else if (Keywords.Equals(name, input.Atoms.ExcludeResultPrefixes)) {
                        compiler.InsertExcludedNamespace(input.Value);
                    }
                    else if (Keywords.Equals(name, input.Atoms.Id)) {
                        // Do nothing here.
                    }
                    else {
                        // We can have version atribute later. For now remember this attribute and continue
                        badAttribute = name;
                    }
                }
                while( input.MoveToNextAttribute());
                input.ToParent();
            }

            if (version == null) {
                throw XsltException.Create(Res.Xslt_MissingAttribute, Keywords.s_Version);
            }

            if (badAttribute != null && ! compiler.ForwardCompatibility) {
                throw XsltException.Create(Res.Xslt_InvalidAttribute, badAttribute, element);
            }
        }

        internal void CompileSingleTemplate(Compiler compiler) {
            NavigatorInput input = compiler.Input;

            //
            // find mandatory version attribute and launch compilation of single template
            //

            string version = null;

            if (input.MoveToFirstAttribute()) {
                do {
                    string nspace = input.NamespaceURI;
                    string name   = input.LocalName;

                    if (Keywords.Equals(nspace, input.Atoms.XsltNamespace) &&
                        Keywords.Equals(name,   input.Atoms.Version)) {
                        version = input.Value;
                    }
                }
                while(input.MoveToNextAttribute());
                input.ToParent();
            }

            if (version == null) {
                if (Keywords.Equals(input.LocalName, input.Atoms.Stylesheet) &&
                    input.NamespaceURI == Keywords.s_WdXslNamespace) {
                    throw XsltException.Create(Res.Xslt_WdXslNamespace);
                }
                throw XsltException.Create(Res.Xslt_WrongStylesheetElement);
            }

            compiler.AddTemplate(compiler.CreateSingleTemplateAction());
        }

        /*
         * CompileTopLevelElements
         */
        protected void CompileDocument(Compiler compiler, bool inInclude) {
            NavigatorInput input = compiler.Input;

            // SkipToElement :
            while (input.NodeType != XPathNodeType.Element) {
                if (! compiler.Advance()) {
                    throw XsltException.Create(Res.Xslt_WrongStylesheetElement);
                }
            }

            Debug.Assert(compiler.Input.NodeType == XPathNodeType.Element);
            if (Keywords.Equals(input.NamespaceURI, input.Atoms.XsltNamespace)) {
                if (
                    ! Keywords.Equals(input.LocalName, input.Atoms.Stylesheet) &&
                    ! Keywords.Equals(input.LocalName, input.Atoms.Transform)
                ) {
                    throw XsltException.Create(Res.Xslt_WrongStylesheetElement);
                }
                compiler.PushNamespaceScope();
                CompileStylesheetAttributes(compiler);
                CompileTopLevelElements(compiler);
                if (! inInclude) {
                    CompileImports(compiler);
                }
            }
            else {
                // single template
                compiler.PushLiteralScope();
                CompileSingleTemplate(compiler);
            }

            compiler.PopScope();
        }

        internal Stylesheet CompileImport(Compiler compiler, Uri uri, int id) {
            NavigatorInput input = compiler.ResolveDocument(uri);
            compiler.PushInputDocument(input);

            try {
                compiler.PushStylesheet(new Stylesheet());
                compiler.Stylesheetid = id;
                CompileDocument(compiler, /*inInclude*/ false);
            }
            catch (XsltCompileException) {
                throw;
            }
            catch (Exception e) {
                throw new XsltCompileException(e, input.BaseURI, input.LineNumber, input.LinePosition);
            }
            finally {
                compiler.PopInputDocument();
            }
            return compiler.PopStylesheet();
        }

        private void CompileImports(Compiler compiler) {
            ArrayList imports = compiler.CompiledStylesheet.Imports;
            // We can't reverce imports order. Template lookup relyes on it after compilation
            int saveStylesheetId = compiler.Stylesheetid;
            for (int i = imports.Count - 1; 0 <= i; i --) {   // Imports should be compiled in reverse order
                Uri uri = imports[i] as Uri;
                Debug.Assert(uri != null);
                imports[i] = CompileImport(compiler, uri, ++ this.maxid);
            }
            compiler.Stylesheetid = saveStylesheetId;
        }

        void CompileInclude(Compiler compiler) {
            Uri uri = compiler.ResolveUri(compiler.GetSingleAttribute(compiler.Input.Atoms.Href));
            string resolved = uri.ToString();
            if (compiler.IsCircularReference(resolved)) {
                throw XsltException.Create(Res.Xslt_CircularInclude, resolved);
            }

            NavigatorInput input = compiler.ResolveDocument(uri);
            compiler.PushInputDocument(input);

            try {
                CompileDocument(compiler, /*inInclude*/ true);
            }
            catch (XsltCompileException) {
                throw;
            }
            catch (Exception e) {
                throw new XsltCompileException(e, input.BaseURI, input.LineNumber, input.LinePosition);
            }
            finally {
                compiler.PopInputDocument();
            }
            CheckEmpty(compiler);
        }

        internal void CompileNamespaceAlias(Compiler compiler) {
            NavigatorInput input   = compiler.Input;
            string         element = input.LocalName;
            string namespace1 = null, namespace2 = null;
            string prefix1 = null   , prefix2 = null;
            if (input.MoveToFirstAttribute()) {
                do {
                    string nspace = input.NamespaceURI;
                    string name   = input.LocalName;

                    if (! Keywords.Equals(nspace, input.Atoms.Empty)) continue;

                    if (Keywords.Equals(name,input.Atoms.StylesheetPrefix)) {
                        prefix1    = input.Value;
                        namespace1 = compiler.GetNsAlias(ref prefix1);
                    }
                    else if (Keywords.Equals(name,input.Atoms.ResultPrefix)){
                        prefix2    = input.Value;
                        namespace2 = compiler.GetNsAlias(ref prefix2);
                    }
                    else {
                        if (! compiler.ForwardCompatibility) {
                            throw XsltException.Create(Res.Xslt_InvalidAttribute, name, element);
                        }
                    }
                }
                while(input.MoveToNextAttribute());
                input.ToParent();
            }

            CheckRequiredAttribute(compiler, namespace1, Keywords.s_StylesheetPrefix);
            CheckRequiredAttribute(compiler, namespace2, Keywords.s_ResultPrefix    );
            CheckEmpty(compiler);

            //String[] resultarray = { prefix2, namespace2 };
            compiler.AddNamespaceAlias( namespace1, new NamespaceInfo(prefix2, namespace2, compiler.Stylesheetid));
        }

        internal void CompileKey(Compiler compiler){
            NavigatorInput input    = compiler.Input;
            string         element  = input.LocalName;
            int            MatchKey = Compiler.InvalidQueryKey;
            int            UseKey   = Compiler.InvalidQueryKey;

            XmlQualifiedName Name = null;
            if (input.MoveToFirstAttribute()) {
                do {
                    string nspace = input.NamespaceURI;
                    string name   = input.LocalName;
                    string value  = input.Value;

                    if (! Keywords.Equals(nspace, input.Atoms.Empty)) continue;

					if (Keywords.Equals(name, input.Atoms.Name)) {
                        Name = compiler.CreateXPathQName(value);
                    }
                    else if (Keywords.Equals(name, input.Atoms.Match)) {
                        MatchKey = compiler.AddQuery(value, /*allowVars:*/false, /*allowKey*/false, /*pattern*/true);
                    }
                    else if (Keywords.Equals(name, input.Atoms.Use)) {
                        UseKey = compiler.AddQuery(value, /*allowVars:*/false, /*allowKey*/false, /*pattern*/false);
                    }
                    else {
                        if (! compiler.ForwardCompatibility) {
                            throw XsltException.Create(Res.Xslt_InvalidAttribute, name, element);
                        }
                    }
                }
                while(input.MoveToNextAttribute());
                input.ToParent();
            }

            CheckRequiredAttribute(compiler, MatchKey != Compiler.InvalidQueryKey, Keywords.s_Match);
            CheckRequiredAttribute(compiler, UseKey   != Compiler.InvalidQueryKey, Keywords.s_Use  );
            CheckRequiredAttribute(compiler, Name     != null                    , Keywords.s_Name );

            compiler.InsertKey(Name, MatchKey, UseKey);
        }

        protected void CompileDecimalFormat(Compiler compiler){
            NumberFormatInfo info   = new NumberFormatInfo();
            DecimalFormat    format = new DecimalFormat(info, '#', '0', ';');
            XmlQualifiedName  Name  = null;
            NavigatorInput   input  = compiler.Input;
            if (input.MoveToFirstAttribute()) {
                do {
                    if (! Keywords.Equals(input.Prefix, input.Atoms.Empty)) continue;

                    string name   = input.LocalName;
                    string value  = input.Value;

                    if (Keywords.Equals(name, input.Atoms.Name)) {
                        Name = compiler.CreateXPathQName(value);
                    }
                    else if (Keywords.Equals(name, input.Atoms.DecimalSeparator)) {
                        info.NumberDecimalSeparator = value;
                    }
                    else if (Keywords.Equals(name, input.Atoms.GroupingSeparator)) {
                        info.NumberGroupSeparator = value;
                    }
                    else if (Keywords.Equals(name, input.Atoms.Infinity)) {
                        info.PositiveInfinitySymbol = value;
                    }
                    else if (Keywords.Equals(name, input.Atoms.MinusSign)) {
                        info.NegativeSign = value;
                    }
                    else if (Keywords.Equals(name, input.Atoms.NaN)) {
                        info.NaNSymbol = value;
                    }
                    else if (Keywords.Equals(name, input.Atoms.Percent)) {
                        info.PercentSymbol = value;
                    }
                    else if (Keywords.Equals(name, input.Atoms.PerMille)) {
                        info.PerMilleSymbol = value;
                    }
                    else if (Keywords.Equals(name, input.Atoms.Digit)) {
                        if (CheckAttribute(value.Length == 1, compiler)) {
                            format.digit = value[0];
                        }
                    }
                    else if (Keywords.Equals(name, input.Atoms.ZeroDigit)) {
                        if (CheckAttribute(value.Length == 1, compiler)) {
                            format.zeroDigit = value[0];
                        }
                    }
                    else if (Keywords.Equals(name, input.Atoms.PatternSeparator)) {
                        if (CheckAttribute(value.Length == 1, compiler)) {
                            format.patternSeparator = value[0];
                        }
                    }
                }
                while(input.MoveToNextAttribute());
                input.ToParent();
            }
            info.NegativeInfinitySymbol = String.Concat(info.NegativeSign, info.PositiveInfinitySymbol);
            if (Name == null) {
                Name = new XmlQualifiedName();
            }
            compiler.AddDecimalFormat(Name, format);
            CheckEmpty(compiler);
        }

        internal bool CheckAttribute(bool valid, Compiler compiler) {
            if (! valid) {
                if (! compiler.ForwardCompatibility) {
                    throw XsltException.Create(Res.Xslt_InvalidAttrValue, compiler.Input.LocalName, compiler.Input.Value);
                }
                return false;
            }
            return true;
        }

        protected void CompileSpace(Compiler compiler, bool preserve){
            String value = compiler.GetSingleAttribute(compiler.Input.Atoms.Elements);
            String[] elements = XmlConvert.SplitString(value);
            for (int i = 0; i < elements.Length; i++){
                double defaultPriority = NameTest(elements[i]);
                compiler.CompiledStylesheet.AddSpace(compiler, elements[i], defaultPriority, preserve);
            }
            CheckEmpty(compiler);
        }

        double NameTest(String name) {
            if (name == "*") {
                return -0.5;
            }
            int idx = name.Length - 2;
            if (0 <= idx && name[idx] == ':' && name[idx + 1] == '*') {
                if (! PrefixQName.ValidatePrefix(name.Substring(0, idx))) {
                    throw XsltException.Create(Res.Xslt_InvalidAttrValue, Keywords.s_Elements, name);
                }
                return -0.25;
            }
            else {
                string prefix, localname;
                PrefixQName.ParseQualifiedName(name, out prefix, out localname);
                return 0;
            }
        }

        protected void CompileTopLevelElements(Compiler compiler) {
            // Navigator positioned at parent root, need to move to child and then back
            if (compiler.Recurse() == false) {
                return;
            }

            NavigatorInput input    = compiler.Input;
            bool notFirstElement    = false;
            do {
                switch (input.NodeType) {
                case XPathNodeType.Element:
                    string name   = input.LocalName;
                    string nspace = input.NamespaceURI;

                    if (Keywords.Equals(nspace, input.Atoms.XsltNamespace)) {
                        if (Keywords.Equals(name, input.Atoms.Import)) {
                            if (notFirstElement) {
                                throw XsltException.Create(Res.Xslt_NotFirstImport);
                            }
                            // We should compile imports in reverse order after all toplevel elements.
                            // remember it now and return to it in CompileImpoorts();
                            Uri uri = compiler.ResolveUri(compiler.GetSingleAttribute(compiler.Input.Atoms.Href));
                            string resolved = uri.ToString();
                            if (compiler.IsCircularReference(resolved)) {
                                throw XsltException.Create(Res.Xslt_CircularInclude, resolved);
                            }
                            compiler.CompiledStylesheet.Imports.Add(uri);
                            CheckEmpty(compiler);
                        }
                        else if (Keywords.Equals(name, input.Atoms.Include)) {
                            notFirstElement = true;
                            CompileInclude(compiler);
                        }
                        else {
                            notFirstElement = true;
                            compiler.PushNamespaceScope();
                            if (Keywords.Equals(name, input.Atoms.StripSpace)) {
                                CompileSpace(compiler, false);
                            }
                            else if (Keywords.Equals(name, input.Atoms.PreserveSpace)) {
                                CompileSpace(compiler, true);
                            }
                            else if (Keywords.Equals(name, input.Atoms.Output)) {
                                CompileOutput(compiler);
                            }
                            else if (Keywords.Equals(name, input.Atoms.Key)) {
                                CompileKey(compiler);
                            }
                            else if (Keywords.Equals(name, input.Atoms.DecimalFormat)) {
                                CompileDecimalFormat(compiler);
                            }
                            else if (Keywords.Equals(name, input.Atoms.NamespaceAlias)) {
                                CompileNamespaceAlias(compiler);
                            }
                            else if (Keywords.Equals(name, input.Atoms.AttributeSet)) {
                                compiler.AddAttributeSet(compiler.CreateAttributeSetAction());
                            }
                            else if (Keywords.Equals(name, input.Atoms.Variable)) {
                                VariableAction action = compiler.CreateVariableAction(VariableType.GlobalVariable);
                                if (action != null) {
                                    AddAction(action);
                                }
                            }
                            else if (Keywords.Equals(name, input.Atoms.Param)) {
                                VariableAction action = compiler.CreateVariableAction(VariableType.GlobalParameter);
                                if (action != null) {
                                    AddAction(action);
                                }
                            }
                            else if (Keywords.Equals(name, input.Atoms.Template)) {
                                compiler.AddTemplate(compiler.CreateTemplateAction());
                            }
                            else {
                                if (!compiler.ForwardCompatibility) {
                                    throw compiler.UnexpectedKeyword();
                                }
                            }
                            compiler.PopScope();
                        }
                    }
                    else if (nspace == input.Atoms.MsXsltNamespace && name == input.Atoms.Script) {
                        AddScript(compiler);
                    }
                    else {
                        if (Keywords.Equals(nspace, input.Atoms.Empty)) {
                            throw XsltException.Create(Res.Xslt_NullNsAtTopLevel, input.Name);
                        }
                        // Ignoring non-recognized namespace per XSLT spec 2.2
                    }
                    break;

                case XPathNodeType.ProcessingInstruction:
                case XPathNodeType.Comment:
                case XPathNodeType.Whitespace:
                case XPathNodeType.SignificantWhitespace:
                    break;

                default:
                    throw XsltException.Create(Res.Xslt_InvalidContents, Keywords.s_Stylesheet);
                }
            }
            while (compiler.Advance());

            compiler.ToParent();
        }

        protected void CompileTemplate(Compiler compiler) {
            do {
                CompileOnceTemplate(compiler);
            }
            while (compiler.Advance());
        }

        protected void CompileOnceTemplate(Compiler compiler) {
            NavigatorInput input = compiler.Input;

            if (input.NodeType == XPathNodeType.Element) {
                string nspace = input.NamespaceURI;

                if (Keywords.Equals(nspace, input.Atoms.XsltNamespace)) {
                    compiler.PushNamespaceScope();
                    CompileInstruction(compiler);
                    compiler.PopScope();
                }
                else {
                    compiler.PushLiteralScope();
                    compiler.InsertExtensionNamespace();
                    if (compiler.IsExtensionNamespace(nspace)) {
                        AddAction(compiler.CreateNewInstructionAction());
                    }
                    else {
                        CompileLiteral(compiler);
                    }
                    compiler.PopScope();
                }
            }
            else {
                CompileLiteral(compiler);
            }
        }

        void CompileInstruction(Compiler compiler) {
            NavigatorInput input  = compiler.Input;
            CompiledAction action = null;

            Debug.Assert(Keywords.Equals(input.NamespaceURI, input.Atoms.XsltNamespace));

            string name = input.LocalName;

            if (Keywords.Equals(name, input.Atoms.ApplyImports)) {
                action = compiler.CreateApplyImportsAction();
            }
            else if (Keywords.Equals(name, input.Atoms.ApplyTemplates)) {
                action = compiler.CreateApplyTemplatesAction();
            }
            else if (Keywords.Equals(name, input.Atoms.Attribute)) {
                action = compiler.CreateAttributeAction();
            }
            else if (Keywords.Equals(name, input.Atoms.CallTemplate)) {
                action = compiler.CreateCallTemplateAction();
            }
            else if (Keywords.Equals(name, input.Atoms.Choose)) {
                action = compiler.CreateChooseAction();
            }
            else if (Keywords.Equals(name, input.Atoms.Comment)) {
                action = compiler.CreateCommentAction();
            }
            else if (Keywords.Equals(name, input.Atoms.Copy)) {
                action = compiler.CreateCopyAction();
            }
            else if (Keywords.Equals(name, input.Atoms.CopyOf)) {
                action = compiler.CreateCopyOfAction();
            }
            else if (Keywords.Equals(name, input.Atoms.Element)) {
                action = compiler.CreateElementAction();
            }
            else if (Keywords.Equals(name, input.Atoms.Fallback)) {
                return;
            }
            else if (Keywords.Equals(name, input.Atoms.ForEach)) {
                action = compiler.CreateForEachAction();
            }
            else if (Keywords.Equals(name, input.Atoms.If)) {
                action = compiler.CreateIfAction(IfAction.ConditionType.ConditionIf);
            }
            else if (Keywords.Equals(name, input.Atoms.Message)) {
                action = compiler.CreateMessageAction();
            }
            else if (Keywords.Equals(name, input.Atoms.Number)) {
                action = compiler.CreateNumberAction();
            }
            else if (Keywords.Equals(name, input.Atoms.ProcessingInstruction)) {
                action = compiler.CreateProcessingInstructionAction();
            }
            else if (Keywords.Equals(name, input.Atoms.Text)) {
                action = compiler.CreateTextAction();
            }
            else if (Keywords.Equals(name, input.Atoms.ValueOf)) {
                action = compiler.CreateValueOfAction();
            }
            else if (Keywords.Equals(name, input.Atoms.Variable)) {
                action = compiler.CreateVariableAction(VariableType.LocalVariable);
            }
            else {
                if (compiler.ForwardCompatibility)
                    action = compiler.CreateNewInstructionAction();
                else
                    throw compiler.UnexpectedKeyword();
            }

            Debug.Assert(action != null);

            AddAction(action);
        }

        void CompileLiteral(Compiler compiler) {
            NavigatorInput input = compiler.Input;

            switch (input.NodeType) {
            case XPathNodeType.Element:
                this.AddEvent(compiler.CreateBeginEvent());
                CompileLiteralAttributesAndNamespaces(compiler);

                if (compiler.Recurse()) {
                    CompileTemplate(compiler);
                    compiler.ToParent();
                }

                this.AddEvent(new EndEvent(XPathNodeType.Element));
                break;

            case XPathNodeType.Text:
            case XPathNodeType.SignificantWhitespace:
                this.AddEvent(compiler.CreateTextEvent());
                break;
            case XPathNodeType.Whitespace:
            case XPathNodeType.ProcessingInstruction:
            case XPathNodeType.Comment:
                break;

            default:
                Debug.Assert(false, "Unexpected node type.");
                break;
            }
        }

        void CompileLiteralAttributesAndNamespaces(Compiler compiler) {
            NavigatorInput input = compiler.Input;

            if (input.Navigator.MoveToAttribute(Keywords.s_UseAttributeSets, input.Atoms.XsltNamespace)) {
                AddAction(compiler.CreateUseAttributeSetsAction());
                input.Navigator.MoveToParent();
            }
            compiler.InsertExcludedNamespace();

            if (input.MoveToFirstNamespace()) {
                do {
                    string uri = input.Value;

                    if (Keywords.Compare(uri, input.Atoms.XsltNamespace)) {
                        continue;
                    }
                    if (
                        compiler.IsExcludedNamespace(uri) ||
                        compiler.IsExtensionNamespace(uri) ||
                        compiler.IsNamespaceAlias(uri)
                    ) {
                            continue;
                    }
                    this.AddEvent(new NamespaceEvent(input));
                }
                while (input.MoveToNextNamespace());
                input.ToParent();
            }

            if (input.MoveToFirstAttribute()) {
                do {

                    // Skip everything from Xslt namespace
                    if (Keywords.Equals(input.NamespaceURI, input.Atoms.XsltNamespace)) {
                        continue;
                    }

                    // Add attribute events
                    this.AddEvent (compiler.CreateBeginEvent());
                    this.AddEvents(compiler.CompileAvt(input.Value));
                    this.AddEvent (new EndEvent(XPathNodeType.Attribute));
                }
                while (input.MoveToNextAttribute());
                input.ToParent();
            }
        }

        void CompileOutput(Compiler compiler) {
            Debug.Assert((object) this == (object) compiler.RootAction);
            compiler.RootAction.Output.Compile(compiler);
        }

        internal void AddAction(Action action) {
            if (this.containedActions == null) {
                this.containedActions = new ArrayList();
            }
            this.containedActions.Add(action);
            lastCopyCodeAction = null;
        }

        private void EnsureCopyCodeAction() {
            if(lastCopyCodeAction == null) {
                CopyCodeAction copyCode = new CopyCodeAction();
                AddAction(copyCode);
                lastCopyCodeAction = copyCode;
            }
        }

        protected void AddEvent(Event copyEvent) {
            EnsureCopyCodeAction();
            lastCopyCodeAction.AddEvent(copyEvent);
        }

        protected void AddEvents(ArrayList copyEvents) {
            EnsureCopyCodeAction();
            lastCopyCodeAction.AddEvents(copyEvents);
        }

        private void AddScript(Compiler compiler) {
            NavigatorInput input = compiler.Input;

            ScriptingLanguage lang = ScriptingLanguage.JScript;
            string implementsNamespace = null;
            if (input.MoveToFirstAttribute()) {
                do {
                    if (input.LocalName == input.Atoms.Language) {
                        string langName = input.Value;
                        if (
                            String.Compare(langName, "jscript"   , StringComparison.OrdinalIgnoreCase) == 0 ||
                            String.Compare(langName, "javascript", StringComparison.OrdinalIgnoreCase) == 0
                        ) {
                            lang = ScriptingLanguage.JScript;
                        } else if (
                            String.Compare(langName, "c#"    , StringComparison.OrdinalIgnoreCase) == 0 ||
                            String.Compare(langName, "csharp", StringComparison.OrdinalIgnoreCase) == 0
                        ) {
                            lang = ScriptingLanguage.CSharp;
                        }
                        else {
                            throw XsltException.Create(Res.Xslt_ScriptInvalidLanguage, langName);
                        }
                    }
                    else if (input.LocalName == input.Atoms.ImplementsPrefix) {
                        if(! PrefixQName.ValidatePrefix(input.Value))  {
                            throw XsltException.Create(Res.Xslt_InvalidAttrValue, input.LocalName, input.Value);
                        }
                        implementsNamespace = compiler.ResolveXmlNamespace(input.Value);
                    }
                }
                while (input.MoveToNextAttribute());
                input.ToParent();
            }
            if (implementsNamespace == null) {
                throw XsltException.Create(Res.Xslt_MissingAttribute, input.Atoms.ImplementsPrefix);
            }
            if (!input.Recurse() || input.NodeType != XPathNodeType.Text) {
                throw XsltException.Create(Res.Xslt_ScriptEmpty);
            }
            compiler.AddScript(input.Value, lang, implementsNamespace, input.BaseURI, input.LineNumber);
            input.ToParent();
        }

        internal override void Execute(Processor processor, ActionFrame frame) {
            Debug.Assert(processor != null && frame != null);

            switch (frame.State) {
            case Initialized:
                if (this.containedActions != null && this.containedActions.Count > 0) {
                    processor.PushActionFrame(frame);
                    frame.State = ProcessingChildren;
                }
                else {
                    frame.Finished();
                }
                break;                              // Allow children to run

            case ProcessingChildren:
                frame.Finished();
                break;

            default:
                Debug.Fail("Invalid Container action execution state");
                break;
            }
        }

        internal Action GetAction(int actionIndex) {
            Debug.Assert(actionIndex == 0 || this.containedActions != null);

            if (this.containedActions != null && actionIndex < this.containedActions.Count) {
                return (Action) this.containedActions[actionIndex];
            }
            else {
                return null;
            }
        }

        internal void CheckDuplicateParams(XmlQualifiedName name) {
            if (this.containedActions != null) {
                foreach(CompiledAction action in this.containedActions) {
                    WithParamAction param = action as WithParamAction;
                    if (param != null && param.Name == name) {
                        throw XsltException.Create(Res.Xslt_DuplicateWithParam, name.ToString());
                    }
                }
            }
        }

        internal override void ReplaceNamespaceAlias(Compiler compiler){
            if (this.containedActions == null) {
                return;
            }
            int count = this.containedActions.Count;
            for(int i= 0; i < this.containedActions.Count; i++) {
                ((Action)this.containedActions[i]).ReplaceNamespaceAlias(compiler);
            }
        }
    }
}