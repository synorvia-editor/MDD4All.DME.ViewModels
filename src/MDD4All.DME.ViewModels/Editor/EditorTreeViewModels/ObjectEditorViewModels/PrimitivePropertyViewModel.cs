using MDD4All.ObjectGraph.Access;
using MDD4All.Reflection;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using Synorvia.UI.DataModels.Tree;
using System;

namespace MDD4All.DME.ViewModels.Editor
{
    public class PrimitivePropertyViewModel : ObjectEditorViewModel
    {
        #region Constructors and Initialization
        public PrimitivePropertyViewModel(ITree tree, Access access, object item, string? title = null, ITreeNode? parent = null, TypeAnalyzer? preAnalyzedResult = null)
            : base(tree, access, item, null, title, parent, preAnalyzedResult) { }

        public PrimitivePropertyViewModel(ITree tree, Access access, Type targetType, string? title = null, ITreeNode? parent = null, TypeAnalyzer? preAnalyzedResult = null)
            : base(tree, access, null, targetType, title, parent, preAnalyzedResult) { }

        public PrimitivePropertyViewModel(ITree tree, Access access, object? item, Type? targetType, string? title = null, ITreeNode? parent = null, TypeAnalyzer? preAnalyzedResult = null)
            : base(tree, access, item, targetType, title, parent, preAnalyzedResult) { }
        #endregion

        #region Logic / Data
        // Unlike a reference type, a new primitive value has to be written back into the parent explicitly.
        public override object? Item
        {
            get
            {
                return base.Item;
            }
            set
            {
                // Asked before anything is written. The view sets and then reads back, so a
                // value turned away here makes the field snap to what is still in the model.
                IReadOnlyList<ValidationAttribute> broken = this.Annotations.Validate(value);

                this.NoteBrokenRules(broken);

                if (broken.Count > 0)
                {
                    // Not taken. Item keeps the value it had.
                    this.OnPropertyChanged(nameof(Item));
                    return;
                }

                if (base.Item != value)
                {
                    base.Item = value;
                    this.OnPropertyChanged(nameof(Item));

                    this.UpdateParentReference();
                }
                else
                {
                    this.OnPropertyChanged(nameof(Item));
                }
            }
        }
        #endregion

        #region UI / Display
        protected override string DefaultTitle
        {
            get
            {
                string result;
                // Using the common base class for ListAccess and ArrayAccess
                if (Access is IndexedAccess indexedAccess)
                {
                    result = (indexedAccess.Index +1 ).ToString();
                }
                else
                {
                    // Fallback to PropertyInfo name or Type name from base class
                    result = base.DefaultTitle;
                }

                return result;
            }
        }
        #endregion
    }
}
