using CommunityToolkit.Mvvm.ComponentModel;
using MDD4All.DME.ViewModels.Annotations;
using System.ComponentModel.DataAnnotations;
using System.Threading.Tasks;
using MDD4All.Reflection;
using MDD4All.ObjectGraph.Access;
using Synorvia.UI.DataModels.Tree;
using System;
using System.Collections;
using System.Collections.ObjectModel;
using System.ComponentModel.DataAnnotations;

namespace MDD4All.DME.ViewModels.Editor
{
    public abstract class ObjectEditorViewModel : ObservableObject, ITreeNode
    {
        #region Constructors and Initialization
        public ObjectEditorViewModel(ITree tree, Access access, object? item, Type? targetType, string? title = "",
                                        ITreeNode? parent = null, TypeAnalyzer? preAnalyzedResult = null)
        {
            this.Access = access;

            // Straight into the field, not through the property. Derived nodes override the
            // setter, and one of them asks Annotations - which is only built at the end of this
            // constructor, because it needs the analyzer that runs below. Nothing here is a user
            // edit either: the value comes out of the model and has no rule to answer to.
            _item = item;

            this.Tree = tree;
            this.Parent = parent;
            this.Children = new ObservableCollection<ITreeNode>();

            EditorState = new EditorState(this);

            /* * NOTE: The following TypeAnalyzer initialization logic is technically redundant when 
             * this ViewModel is created via ReferenceEditorViewModel.CreateChildViewModel, 
             * as the factory already ensures a valid TypeAnalyzer is provided. 
   
             * It is kept here as a safety measure (fallback) to guarantee that the TypeAnalyzer 
             * is always correctly initialized, even if the constructor is called directly 
             * or the factory's pre-analysis result is missing.
            */

            if (preAnalyzedResult != null)
            {
                this.TypeAnalyzer = new TypeAnalyzer(preAnalyzedResult);
            }
            else
            {
                TypeAnalyzer = new TypeAnalyzer();
                //type Analyzer is set new when Type is setted 
                if (this.Item != null)
                {
                    this.TypeAnalyzer.Analyze(this.Item);
                }
                else if (targetType != null)
                {
                    this.TypeAnalyzer.Analyze(targetType);
                }
                else
                {
                    this.TypeAnalyzer.Analyze(typeof(object));
                }
            }

            // After the analyzer, because Type reads from it. A node hangs on a property or on
            // nothing - the root, a list element, a dictionary entry - and then its type is what
            // carries the annotations.
            if (access is PropertyAccess annotatedProperty)
            {
                Annotations = new MemberAnnotations(annotatedProperty.PropertyInfo);
            }
            else
            {
                Annotations = new MemberAnnotations(this.Type);
            }

            if (!string.IsNullOrEmpty(title))
            {
                Title = title;
            }
        }

        public ObjectEditorViewModel(ITree tree, Access access, TypeAnalyzer preAnalyzedResult, string? title = null, ITreeNode? parent = null)
            : this(tree, access, null, null, title, parent, preAnalyzedResult) { }

        public ObjectEditorViewModel(ITree tree, Access access, object item, TypeAnalyzer preAnalyzedResult, string? title = null, ITreeNode? parent = null)
            : this(tree, access, item, null, title, parent, preAnalyzedResult) { }

        public ObjectEditorViewModel(ITree tree, Access access, object item, string? title = null, ITreeNode? parent = null)
            : this(tree, access, item, null, title, parent, null) { }

        public ObjectEditorViewModel(ITree tree, Access access, Type targetType, string? title = null, ITreeNode? parent = null)
            : this(tree, access, null, targetType, title, parent, null) { }
        #endregion

        #region Logic / Data

        public Access Access { get; set; } = null!;

        // What the model declares about this place. Read once - see MemberAnnotations.
        public MemberAnnotations Annotations { get; private set; } = null!;

        private IReadOnlyList<ValidationAttribute> _brokenRules = new List<ValidationAttribute>();

        // Which rules the last value offered here broke. The attributes, not sentences - a
        // sentence is worded in whatever language is set when it is asked for, and that changes
        // while the editor runs. The view puts them into words, see ValidationTextProvider.
        public IReadOnlyList<ValidationAttribute> BrokenRules
        {
            get
            {
                return _brokenRules;
            }
        }

        // Would this value be taken? Asked while someone is still typing, where the field should
        // redden but no sentence should appear yet. Keeps nothing.
        public bool IsValid(object? candidate)
        {
            return Annotations.Validate(candidate).Count == 0;
        }

        // How long a reason stays on screen before it fades out on its own.
        private static readonly TimeSpan MessageLifetime = TimeSpan.FromSeconds(8);

        // Counts the attempts, so a timeout only clears the message it was started for. Type
        // again and the older timeout finds a newer number and does nothing.
        private int _messageAttempt;

        // Kept separate from the value itself: the view sets this when it wants the reason shown.
        protected void NoteBrokenRules(IReadOnlyList<ValidationAttribute> broken)
        {
            _brokenRules = broken;

            OnPropertyChanged(nameof(BrokenRules));

            if (broken.Count > 0)
            {
                ForgetMessagesLater();
            }
        }

        private async void ForgetMessagesLater()
        {
            int attempt = ++_messageAttempt;

            await Task.Delay(MessageLifetime);

            // Nothing happened in between, so the reason has been read or ignored long enough.
            if (attempt == _messageAttempt)
            {
                ClearBrokenRules();
            }
        }

        // Typing is a new attempt, so the reason the last one failed goes away.
        public void ClearBrokenRules()
        {
            if (_brokenRules.Count > 0)
            {
                _brokenRules = new List<ValidationAttribute>();

                // Invalidates a timeout still waiting for the attempt just cleared.
                _messageAttempt++;

                OnPropertyChanged(nameof(BrokenRules));
            }
        }

        private object? _item;

        // Virtual so PrimitivePropertyViewModel can also write the new value back into its parent.
        public virtual object? Item
        {
            get
            {
                return _item;
            }
            set
            {
                if (_item != value)
                {
                    bool wasNullBefore = (_item == null);

                    _item = value;

                    bool isNullNow = (_item == null);

                    OnPropertyChanged(nameof(Item));

                    // Only notify IsNull when its own value actually flips, not on every Item change.
                    if (wasNullBefore != isNullNow)
                    {
                        OnPropertyChanged(nameof(IsNull));
                    }
                }
            }
        }

        public Type Type
        {
            get
            {
                Type result = typeof(object);
                if (TypeAnalyzer != null && TypeAnalyzer.AnalyzeType != null)
                {
                    result = TypeAnalyzer.AnalyzeType;
                }
                return result;
            }
            protected set
            {
                if (value != null)
                {
                    TypeAnalyzer.Analyze(value);
                }
                else
                {
                    TypeAnalyzer.Analyze(typeof(object));
                }
            }
        }

        public TypeCategory TypeCategory
        {
            get
            {
                return TypeAnalyzer.TypeCategory;
            }
        }

        public TypeAnalyzer TypeAnalyzer { get; set; } = null!;

        public bool IsReadOnly
        {
            get
            {
                return !Access.CanWrite;
            }
        }

        public bool IsNull
        {
            get
            {
                bool result = false;
                if (this.Item == null)
                {
                    result = true;
                }
                return result;
            }
        }

        public ITreeNode? Parent { get; set; }

        public ObservableCollection<ITreeNode> Children { get; set; } = null!;

        private ITree? _tree;

        public ITree? Tree
        {
            get
            {
                return _tree;
            }
            set
            {
                _tree = value;
            }
        }

        public int Index
        {
            get
            {
                int result = 0;

                if (Parent != null)
                {
                    int counter = 0;
                    foreach (ITreeNode child in Parent.Children)
                    {
                        if (child == this)
                        {
                            result = counter;
                            break;
                        }
                        counter++;
                    }

                }

                return result;
            }
        }

        public bool HasChildNodes
        {
            get
            {
                return Children.Count > 0;
            }
        }

        // Wraps the magic string so it exists in one place instead of being retyped at every call site.
        public void RaiseStateChanged()
        {
            OnPropertyChanged("StateChanged");

            if (_tree is ObjectTreeViewModel objectTree)
            {
                objectTree.RaiseTreeChanged();
            }
        }

        // Required by the ITreeNode interface; never raised by this editor.
        public event EventHandler? TreeStateChanged;

        // Writes this.Item back into wherever Access says it lives in the parent - a
        // property, an indexed slot in a List/Array, or a dictionary entry. Shared by
        // PrimitivePropertyViewModel (a value replacing itself) and the Reference/
        // Collection editors (a newly created instance replacing a null slot).
        protected void UpdateParentReference()
        {
            if (this.Parent is ObjectEditorViewModel parentVM)
            {
                if (parentVM is DictionaryEntryViewModel entryParent)
                {
                    entryParent.ChangeChild(this.Access, this.Item);
                }
                else if (parentVM.Item != null)
                {
                    if (this.Access is PropertyAccess propertyAccess)
                    {
                        propertyAccess.PropertyInfo.SetValue(parentVM.Item, this.Item);
                    }
                    else if (this.Access is IndexedAccess indexedAccess && parentVM.Item is IList list)
                    {
                        list[indexedAccess.Index] = this.Item;
                    }
                }

                parentVM.RaiseStateChanged();
            }
        }

        #endregion

        #region UI / Display

        public EditorState EditorState { get; private set; }

        // Short, technical type label ("List"/"Array"/"Dictionary"/"Object") for
        // readers who want to know the underlying .NET shape at a glance.
        public string? TypeBadgeText
        {
            get
            {
                string? result = null;

                switch (TypeCategory)
                {
                    case TypeCategory.IList:
                        result = "List";
                        break;

                    case TypeCategory.Array:
                        result = "Array";
                        break;

                    case TypeCategory.IDictionary:
                        result = "Dictionary";
                        break;

                    case TypeCategory.None:
                        result = "Object";
                        break;
                }

                return result;
            }
        }

        // Single-letter symbol for the Explorer tree, mirroring TypeBadgeText's
        // categories in a more compact form ("s" simple, "c" collection, "O" object).
        public string TypeSymbol
        {
            get
            {
                string result = "";

                switch (TypeCategory)
                {
                    case TypeCategory.Simple:
                    case TypeCategory.SimpleNullable:
                        result = "s";
                        break;

                    case TypeCategory.IList:
                    case TypeCategory.Array:
                    case TypeCategory.IDictionary:
                        result = "c";
                        break;

                    case TypeCategory.None:
                        result = "O";
                        break;
                }

                return result;
            }
        }

        private string? _title;

        public string Title
        {
            get
            {
                string result = string.Empty;
                if (!string.IsNullOrEmpty(_title))
                {
                    result = _title;
                }
                else
                {
                    result = DefaultTitle;
                }
                return result;
            }
            set
            {
                _title = value;
                OnPropertyChanged(nameof(Title));
            }
        }

        // What the UI shows almost everywhere: Title falls back to this derived
        // name whenever no explicit title was set (only dictionary Key/Value
        // children get one). Derived from how the node hangs in its parent,
        // parallel to the Access hierarchy:
        // - PropertyAccess:   property name, preferring its [Display] label
        // - IndexedAccess:    "N. <name>" (ReferenceEditorViewModel) or just "N" (PrimitivePropertyViewModel)
        // - Dictionary entry: "[Key]" (DictionaryEntryViewModel)
        // - Root / else:      type name, preferring its [Display] label
        protected virtual string DefaultTitle
        {
            get
            {
                // Prefer the [Display] label from the data model.
                string? result = GetDisplayAnnotationName();

                // No label? Then the plain name of what this node hangs on.
                if (result == null)
                {
                    if (Access is PropertyAccess propertyAccess)
                    {
                        result = propertyAccess.PropertyInfo.Name;
                    }
                    else
                    {
                        result = Type.Name;
                    }
                }

                return result;
            }
        }

        // The label the model declares, translated into the picked language; null when there
        // is none, or when the setting says to show the code's own names instead.
        private string? GetDisplayAnnotationName()
        {
            string? result = null;

            if (Tree is ObjectTreeViewModel objectTree)
            {
                if (!objectTree.ShowAnnotationNames)
                {
                    return null;
                }

                if (Annotations.Display != null && objectTree.AnnotationTexts != null)
                {
                    // Display.Name is a resource key here, not a text - the provider turns it
                    // into one, freshly on every render and in whatever language is set.
                    result = objectTree.AnnotationTexts.Resolve(Annotations.Display);
                }
            }

            return result;
        }

        // Which input control fits this value, e.g. "MultilineText".
        public string? DataTypeAnnotation
        {
            get
            {
                string? result = null;

                if (Annotations.DataType != null)
                {
                    result = Annotations.DataType.GetDataTypeName();
                }

                return result;
            }
        }

        // Required by the ITreeNode interface; the actual expand state lives on EditorState.IsExpanded instead.
        public bool IsExpanded { get; set; }

        public bool IsSelected
        {
            get
            {
                bool result = false;
                if (Tree != null)
                {
                    result = Tree.SelectedNode == this;
                }
                return result;
            }
            set
            {
                // No-op: ITreeNode requires a setter, but selection is actually driven by setting Tree.SelectedNode.
            }
        }

        public bool IsLoading
        {
            get
            {
                return false;
            }
        }

        // Hierarchical position path from the root, e.g. "1.2.3" - the index numbers the ShowIndexNumbers setting toggles on and off.
        public string Level
        {
            get
            {
                string result = "";

                result = "" + (Index + 1);

                ITreeNode item = this;

                while (item.Parent != null)
                {
                    result = (item.Parent.Index + 1) + "." + result;
                    item = item.Parent;
                }

                return result;
            }
        }

        public bool IsDisabled { get; set; } = false;

        public string DragDropOperationInformation { get; set; } = string.Empty;

        #endregion
    }
}
