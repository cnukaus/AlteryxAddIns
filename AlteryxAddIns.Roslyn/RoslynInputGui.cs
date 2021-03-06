﻿using System;
using System.Threading.Tasks;

using OmniBus.Framework.ConfigWindows;

namespace JDunkerley.AlteryxAddIns.Roslyn
{
    /// <summary>
    /// Config Window for <see cref="RoslynInput"/>
    /// </summary>
    public class RoslynInputGui : BaseGui<RoslynInputConfig>
    {
        private readonly RoslynEditor _editor;

        public RoslynInputGui(Func<string, string> getCodeBlock)
        {
            this.GetCodeBlock = getCodeBlock;

            this._editor = new RoslynEditor();
            this._editor.CodeChanged += this.EditorOnCodeChanged;
            this.AddControl(this._editor);
        }

        public Func<string, string> GetCodeBlock { get; }

        private async void EditorOnCodeChanged(object sender, EventArgs eventArgs)
        {
            this.Config.LambdaCode = this._editor.Code;
            var lambda = this._editor.Code;

            var code = this.GetCodeBlock(lambda);
            var result = await Task.Run(() => Compiler.Compile(code));

            if (this._editor.Code == lambda)
            {
                this._editor.SetMessages(result.Messages, 10, 13);
            }
        }

        /// <summary>
        /// Called when the <see cref="BaseGui{T}.Config"/> object is set up
        /// </summary>
        protected override void OnObjectSet()
        {
            this._editor.Code = this.Config.LambdaCode;
        }
    }
}