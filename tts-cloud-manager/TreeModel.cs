using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Input;

// From https://www.codeproject.com/Articles/30721/WPF-TreeListView-Control
// under Apache License 2.0
namespace tts_cloud_manager.tree
{
    public interface ITreeModel
    {
        /// <summary>
        /// Get list of children of the specified parent
        /// </summary>
        IEnumerable GetChildren(object parent);

        /// <summary>
        /// returns wheather specified parent has any children or not.
        /// </summary>
        bool HasChildren(object parent);
    }

    public class TreeList : ListView
    {
        #region Properties

        /// <summary>
        /// Internal collection of rows representing visible nodes, actually displayed in the ListView
        /// </summary>
        internal ObservableCollectionAdv<TreeNode> Rows
        {
            get;
            private set;
        }


        private ITreeModel _model;
        public ITreeModel Model
        {
            get { return _model; }
            set
            {
                if (_model != value)
                {
                    _model = value;
                    _root.Children.Clear();
                    Rows.Clear();
                    CreateChildrenNodes(_root);
                }
            }
        }

        private TreeNode _root;
        internal TreeNode Root
        {
            get { return _root; }
        }

        public ReadOnlyCollection<TreeNode> Nodes
        {
            get { return Root.Nodes; }
        }

        internal TreeNode PendingFocusNode
        {
            get;
            set;
        }

        public ICollection<TreeNode> SelectedNodes
        {
            get
            {
                return SelectedItems.Cast<TreeNode>().ToArray();
            }
        }

        public TreeNode SelectedNode
        {
            get
            {
                if (SelectedItems.Count > 0)
                    return SelectedItems[0] as TreeNode;
                else
                    return null;
            }
        }
        #endregion

        public TreeList()
        {
            Rows = new ObservableCollectionAdv<TreeNode>();
            _root = new TreeNode(this, null);
            _root.IsExpanded = true;
            ItemsSource = Rows;
            ItemContainerGenerator.StatusChanged += ItemContainerGeneratorStatusChanged;
        }

        void ItemContainerGeneratorStatusChanged(object sender, EventArgs e)
        {
            if (ItemContainerGenerator.Status == GeneratorStatus.ContainersGenerated && PendingFocusNode != null)
            {
                var item = ItemContainerGenerator.ContainerFromItem(PendingFocusNode) as TreeListItem;
                if (item != null)
                    item.Focus();
                PendingFocusNode = null;
            }
        }

        protected override DependencyObject GetContainerForItemOverride()
        {
            return new TreeListItem();
        }

        protected override bool IsItemItsOwnContainerOverride(object item)
        {
            return item is TreeListItem;
        }

        protected override void PrepareContainerForItemOverride(DependencyObject element, object item)
        {
            var ti = element as TreeListItem;
            var node = item as TreeNode;
            if (ti != null && node != null)
            {
                ti.Node = item as TreeNode;
                base.PrepareContainerForItemOverride(element, node.Tag);
            }
        }

        internal void SetIsExpanded(TreeNode node, bool value)
        {
            if (value)
            {
                if (!node.IsExpandedOnce)
                {
                    node.IsExpandedOnce = true;
                    node.AssignIsExpanded(value);
                    CreateChildrenNodes(node);
                }
                else
                {
                    node.AssignIsExpanded(value);
                    CreateChildrenRows(node);
                }
            }
            else
            {
                DropChildrenRows(node, false);
                node.AssignIsExpanded(value);
            }
        }

        internal void CreateChildrenNodes(TreeNode node)
        {
            var children = GetChildren(node);
            if (children != null)
            {
                int rowIndex = Rows.IndexOf(node);
                node.ChildrenSource = children as INotifyCollectionChanged;
                foreach (object obj in children)
                {
                    TreeNode child = new TreeNode(this, obj);
                    child.HasChildren = HasChildren(child);
                    node.Children.Add(child);
                }
                Rows.InsertRange(rowIndex + 1, node.Children.ToArray());
            }
        }

        private void CreateChildrenRows(TreeNode node)
        {
            int index = Rows.IndexOf(node);
            if (index >= 0 || node == _root) // ignore invisible nodes
            {
                var nodes = node.AllVisibleChildren.ToArray();
                Rows.InsertRange(index + 1, nodes);
            }
        }

        internal void DropChildrenRows(TreeNode node, bool removeParent)
        {
            int start = Rows.IndexOf(node);
            if (start >= 0 || node == _root) // ignore invisible nodes
            {
                int count = node.VisibleChildrenCount;
                if (removeParent)
                    count++;
                else
                    start++;
                Rows.RemoveRange(start, count);
            }
        }

        private IEnumerable GetChildren(TreeNode parent)
        {
            if (Model != null)
                return Model.GetChildren(parent.Tag);
            else
                return null;
        }

        private bool HasChildren(TreeNode parent)
        {
            if (parent == Root)
                return true;
            else if (Model != null)
                return Model.HasChildren(parent.Tag);
            else
                return false;
        }

        internal void InsertNewNode(TreeNode parent, object tag, int rowIndex, int index)
        {
            TreeNode node = new TreeNode(this, tag);
            if (index >= 0 && index < parent.Children.Count)
                parent.Children.Insert(index, node);
            else
            {
                index = parent.Children.Count;
                parent.Children.Add(node);
            }
            Rows.Insert(rowIndex + index + 1, node);
        }
    }

    public class TreeListItem : ListViewItem, INotifyPropertyChanged
    {
        #region Properties

        private TreeNode _node;
        public TreeNode Node
        {
            get { return _node; }
            internal set
            {
                _node = value;
                OnPropertyChanged("Node");
            }
        }

        #endregion

        public TreeListItem()
        {
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            if (Node != null)
            {
                switch (e.Key)
                {
                    case Key.Right:
                        e.Handled = true;
                        if (!Node.IsExpanded)
                        {
                            Node.IsExpanded = true;
                            ChangeFocus(Node);
                        }
                        else if (Node.Children.Count > 0)
                            ChangeFocus(Node.Children[0]);
                        break;

                    case Key.Left:

                        e.Handled = true;
                        if (Node.IsExpanded && Node.IsExpandable)
                        {
                            Node.IsExpanded = false;
                            ChangeFocus(Node);
                        }
                        else
                            ChangeFocus(Node.Parent);
                        break;

                    case Key.Subtract:
                        e.Handled = true;
                        Node.IsExpanded = false;
                        ChangeFocus(Node);
                        break;

                    case Key.Add:
                        e.Handled = true;
                        Node.IsExpanded = true;
                        ChangeFocus(Node);
                        break;
                }
            }

            if (!e.Handled)
                base.OnKeyDown(e);
        }

        private void ChangeFocus(TreeNode node)
        {
            var tree = node.Tree;
            if (tree != null)
            {
                var item = tree.ItemContainerGenerator.ContainerFromItem(node) as TreeListItem;
                if (item != null)
                    item.Focus();
                else
                    tree.PendingFocusNode = node;
            }
        }

        #region INotifyPropertyChanged Members

        public event PropertyChangedEventHandler PropertyChanged;

        private void OnPropertyChanged(string name)
        {
            if (PropertyChanged != null)
                PropertyChanged(this, new PropertyChangedEventArgs(name));
        }

        #endregion
    }


    public sealed class TreeNode : INotifyPropertyChanged
    {
        #region NodeCollection
        private class NodeCollection : Collection<TreeNode>
        {
            private TreeNode _owner;

            public NodeCollection(TreeNode owner)
            {
                _owner = owner;
            }

            protected override void ClearItems()
            {
                while (this.Count != 0)
                    this.RemoveAt(this.Count - 1);
            }

            protected override void InsertItem(int index, TreeNode item)
            {
                if (item == null)
                    throw new ArgumentNullException("item");

                if (item.Parent != _owner)
                {
                    if (item.Parent != null)
                        item.Parent.Children.Remove(item);
                    item._parent = _owner;
                    item._index = index;
                    for (int i = index; i < Count; i++)
                        this[i]._index++;
                    base.InsertItem(index, item);
                }
            }

            protected override void RemoveItem(int index)
            {
                TreeNode item = this[index];
                item._parent = null;
                item._index = -1;
                for (int i = index + 1; i < Count; i++)
                    this[i]._index--;
                base.RemoveItem(index);
            }

            protected override void SetItem(int index, TreeNode item)
            {
                if (item == null)
                    throw new ArgumentNullException("item");
                RemoveAt(index);
                InsertItem(index, item);
            }
        }
        #endregion

        #region INotifyPropertyChanged Members

        public event PropertyChangedEventHandler PropertyChanged;
        private void OnPropertyChanged(string name)
        {
            if (PropertyChanged != null)
                PropertyChanged(this, new PropertyChangedEventArgs(name));
        }

        #endregion

        #region Properties

        private TreeList _tree;
        internal TreeList Tree
        {
            get { return _tree; }
        }

        private INotifyCollectionChanged _childrenSource;
        internal INotifyCollectionChanged ChildrenSource
        {
            get { return _childrenSource; }
            set
            {
                if (_childrenSource != null)
                    _childrenSource.CollectionChanged -= ChildrenChanged;

                _childrenSource = value;

                if (_childrenSource != null)
                    _childrenSource.CollectionChanged += ChildrenChanged;
            }
        }

        private int _index = -1;
        public int Index
        {
            get
            {
                return _index;
            }
        }

        /// <summary>
        /// Returns true if all parent nodes of this node are expanded.
        /// </summary>
        internal bool IsVisible
        {
            get
            {
                TreeNode node = _parent;
                while (node != null)
                {
                    if (!node.IsExpanded)
                        return false;
                    node = node.Parent;
                }
                return true;
            }
        }

        public bool IsExpandedOnce
        {
            get;
            internal set;
        }

        public bool HasChildren
        {
            get;
            internal set;
        }

        private bool _isExpanded;
        public bool IsExpanded
        {
            get { return _isExpanded; }
            set
            {
                if (value != IsExpanded)
                {
                    Tree.SetIsExpanded(this, value);
                    OnPropertyChanged("IsExpanded");
                    OnPropertyChanged("IsExpandable");
                }
            }
        }

        internal void AssignIsExpanded(bool value)
        {
            _isExpanded = value;
        }

        public bool IsExpandable
        {
            get
            {
                return (HasChildren && !IsExpandedOnce) || Nodes.Count > 0;
            }
        }

        private bool _isSelected;
        public bool IsSelected
        {
            get { return _isSelected; }
            set
            {
                if (value != _isSelected)
                {
                    _isSelected = value;
                    OnPropertyChanged("IsSelected");
                }
            }
        }


        private TreeNode _parent;
        public TreeNode Parent
        {
            get { return _parent; }
        }

        public int Level
        {
            get
            {
                if (_parent == null)
                    return -1;
                else
                    return _parent.Level + 1;
            }
        }

        public TreeNode PreviousNode
        {
            get
            {
                if (_parent != null)
                {
                    int index = Index;
                    if (index > 0)
                        return _parent.Nodes[index - 1];
                }
                return null;
            }
        }

        public TreeNode NextNode
        {
            get
            {
                if (_parent != null)
                {
                    int index = Index;
                    if (index < _parent.Nodes.Count - 1)
                        return _parent.Nodes[index + 1];
                }
                return null;
            }
        }

        internal TreeNode BottomNode
        {
            get
            {
                TreeNode parent = this.Parent;
                if (parent != null)
                {
                    if (parent.NextNode != null)
                        return parent.NextNode;
                    else
                        return parent.BottomNode;
                }
                return null;
            }
        }

        internal TreeNode NextVisibleNode
        {
            get
            {
                if (IsExpanded && Nodes.Count > 0)
                    return Nodes[0];
                else
                {
                    TreeNode nn = NextNode;
                    if (nn != null)
                        return nn;
                    else
                        return BottomNode;
                }
            }
        }

        public int VisibleChildrenCount
        {
            get
            {
                return AllVisibleChildren.Count();
            }
        }

        public IEnumerable<TreeNode> AllVisibleChildren
        {
            get
            {
                int level = this.Level;
                TreeNode node = this;
                while (true)
                {
                    node = node.NextVisibleNode;
                    if (node != null && node.Level > level)
                        yield return node;
                    else
                        break;
                }
            }
        }

        private object _tag;
        public object Tag
        {
            get { return _tag; }
        }

        private Collection<TreeNode> _children;
        internal Collection<TreeNode> Children
        {
            get { return _children; }
        }

        private ReadOnlyCollection<TreeNode> _nodes;
        public ReadOnlyCollection<TreeNode> Nodes
        {
            get { return _nodes; }
        }

        #endregion

        internal TreeNode(TreeList tree, object tag)
        {
            if (tree == null)
                throw new ArgumentNullException("tree");

            _tree = tree;
            _children = new NodeCollection(this);
            _nodes = new ReadOnlyCollection<TreeNode>(_children);
            _tag = tag;
        }

        public override string ToString()
        {
            if (Tag != null)
                return Tag.ToString();
            else
                return base.ToString();
        }

        void ChildrenChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
            switch (e.Action)
            {
                case NotifyCollectionChangedAction.Add:
                    if (e.NewItems != null)
                    {
                        int index = e.NewStartingIndex;
                        int rowIndex = Tree.Rows.IndexOf(this);
                        foreach (object obj in e.NewItems)
                        {
                            Tree.InsertNewNode(this, obj, rowIndex, index);
                            index++;
                        }
                    }
                    break;

                case NotifyCollectionChangedAction.Remove:
                    if (Children.Count > e.OldStartingIndex)
                        RemoveChildAt(e.OldStartingIndex);
                    break;

                case NotifyCollectionChangedAction.Move:
                case NotifyCollectionChangedAction.Replace:
                case NotifyCollectionChangedAction.Reset:
                    while (Children.Count > 0)
                        RemoveChildAt(0);
                    Tree.CreateChildrenNodes(this);
                    break;
            }
            HasChildren = Children.Count > 0;
            OnPropertyChanged("IsExpandable");
        }

        private void RemoveChildAt(int index)
        {
            var child = Children[index];
            Tree.DropChildrenRows(child, true);
            ClearChildrenSource(child);
            Children.RemoveAt(index);
        }

        private void ClearChildrenSource(TreeNode node)
        {
            node.ChildrenSource = null;
            foreach (var n in node.Children)
                ClearChildrenSource(n);
        }
    }


    public class ObservableCollectionAdv<T> : ObservableCollection<T>
    {
        public void RemoveRange(int index, int count)
        {
            this.CheckReentrancy();
            var items = this.Items as List<T>;
            items.RemoveRange(index, count);
            OnReset();
        }

        public void InsertRange(int index, IEnumerable<T> collection)
        {
            this.CheckReentrancy();
            var items = this.Items as List<T>;
            items.InsertRange(index, collection);
            OnReset();
        }

        private void OnReset()
        {
            OnPropertyChanged("Count");
            OnPropertyChanged("Item[]");
            OnCollectionChanged(new NotifyCollectionChangedEventArgs(
                NotifyCollectionChangedAction.Reset));
        }

        private void OnPropertyChanged(string propertyName)
        {
            OnPropertyChanged(new PropertyChangedEventArgs(propertyName));
        }
    }

    /// <summary>
    /// Convert Level to left margin
    /// </summary>
    internal class LevelToIndentConverter : IValueConverter
    {
        private const double IndentSize = 19.0;

        public object Convert(object o, Type type, object parameter, CultureInfo culture)
        {
            return new Thickness((int)o * IndentSize, 0, 0, 0);
        }

        public object ConvertBack(object o, Type type, object parameter, CultureInfo culture)
        {
            throw new NotSupportedException();
        }
    }

    internal class CanExpandConverter : IValueConverter
    {
        public object Convert(object o, Type type, object parameter, CultureInfo culture)
        {
            if ((bool)o)
                return Visibility.Visible;
            else
                return Visibility.Hidden;
        }

        public object ConvertBack(object o, Type type, object parameter, CultureInfo culture)
        {
            throw new NotSupportedException();
        }
    }

    public class RowExpander : Control
    {
        static RowExpander()
        {
            DefaultStyleKeyProperty.OverrideMetadata(typeof(RowExpander), new FrameworkPropertyMetadata(typeof(RowExpander)));
        }
    }
}
