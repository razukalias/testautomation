using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace Test_Automation
{
    public class PlanNode
    {
        public string Type { get; }
        public string Title { get; }
        public PlanNode? Parent { get; set; }
        public ObservableCollection<PlanNode> Children { get; } = new ObservableCollection<PlanNode>();

        public string DisplayName => Title;

        public PlanNode(string type, string title)
        {
            Type = type;
            Title = title;
        }
    }

    public partial class MainWindow : Window
    {
        public ObservableCollection<PlanNode> RootNodes { get; } = new ObservableCollection<PlanNode>();

        private Point _dragStartPoint;
        private PlanNode? _draggedNode;

        private static readonly string[] StepTypes =
        {
            "Http", "Sql", "Assert", "VariableExtractor", "Script", "Timer"
        };

        public MainWindow()
        {
            InitializeComponent();
            DataContext = this;
        }

        private void AddTestPlanButton_Click(object sender, RoutedEventArgs e)
        {
            if (RootNodes.Any(node => node.Type == "TestPlan"))
            {
                MessageBox.Show("Only one TestPlan root is allowed.", "Info", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            RootNodes.Add(new PlanNode("TestPlan", "TestPlan"));
        }

        private void PlanTreeView_PreviewMouseRightButtonDown(object sender, MouseButtonEventArgs e)
        {
            var item = FindParentTreeViewItem(e.OriginalSource as DependencyObject);
            if (item == null)
            {
                return;
            }

            item.IsSelected = true;
            if (item.DataContext is not PlanNode selectedNode)
            {
                return;
            }

            item.ContextMenu = BuildContextMenuForNode(selectedNode);
        }

        private ContextMenu BuildContextMenuForNode(PlanNode selectedNode)
        {
            var menu = new ContextMenu();

            foreach (var childType in GetAllowedChildren(selectedNode.Type))
            {
                var addItem = new MenuItem { Header = $"Add {childType}" };
                addItem.Click += (_, _) => AddChildNode(selectedNode, childType);
                menu.Items.Add(addItem);
            }

            if (menu.Items.Count > 0)
            {
                menu.Items.Add(new Separator());
            }

            var removeItem = new MenuItem { Header = "Delete" };
            removeItem.Click += (_, _) => RemoveNode(selectedNode);
            menu.Items.Add(removeItem);

            return menu;
        }

        private static string[] GetAllowedChildren(string parentType)
        {
            if (parentType == "TestPlan")
            {
                return new[] { "Threads" };
            }

            if (parentType == "Threads" || parentType == "If" || parentType == "Loop" || parentType == "Foreach")
            {
                return new[] { "Config", "If", "Loop", "Foreach" }.Concat(StepTypes).ToArray();
            }

            return Array.Empty<string>();
        }

        private void AddChildNode(PlanNode parent, string childType)
        {
            var child = new PlanNode(childType, childType) { Parent = parent };
            parent.Children.Add(child);
        }

        private void RemoveNode(PlanNode node)
        {
            if (node.Parent == null)
            {
                RootNodes.Remove(node);
                return;
            }

            node.Parent.Children.Remove(node);
        }

        private void PlanTreeView_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            _dragStartPoint = e.GetPosition(null);
            var item = FindParentTreeViewItem(e.OriginalSource as DependencyObject);
            _draggedNode = item?.DataContext as PlanNode;
        }

        private void PlanTreeView_PreviewMouseMove(object sender, MouseEventArgs e)
        {
            if (e.LeftButton != MouseButtonState.Pressed || _draggedNode == null)
            {
                return;
            }

            var mousePos = e.GetPosition(null);
            var diff = _dragStartPoint - mousePos;

            if (Math.Abs(diff.X) < SystemParameters.MinimumHorizontalDragDistance &&
                Math.Abs(diff.Y) < SystemParameters.MinimumVerticalDragDistance)
            {
                return;
            }

            DragDrop.DoDragDrop(PlanTreeView, new DataObject(typeof(PlanNode), _draggedNode), DragDropEffects.Move);
            _draggedNode = null;
        }

        private void PlanTreeView_DragOver(object sender, DragEventArgs e)
        {
            if (!TryGetDragNodes(e, out var sourceNode, out var targetNode))
            {
                e.Effects = DragDropEffects.None;
                e.Handled = true;
                return;
            }

            var canReorder = CanReorder(sourceNode, targetNode);
            e.Effects = canReorder ? DragDropEffects.Move : DragDropEffects.None;
            e.Handled = true;
        }

        private void PlanTreeView_Drop(object sender, DragEventArgs e)
        {
            if (!TryGetDragNodes(e, out var sourceNode, out var targetNode))
            {
                e.Handled = true;
                return;
            }

            if (!CanReorder(sourceNode, targetNode))
            {
                e.Handled = true;
                return;
            }

            var sourceParent = sourceNode.Parent;

            if (sourceParent == null)
            {
                ReorderInCollection(RootNodes, sourceNode, targetNode);
                e.Handled = true;
                return;
            }

            ReorderInCollection(sourceParent.Children, sourceNode, targetNode);
            e.Handled = true;
        }

        private bool TryGetDragNodes(DragEventArgs e, out PlanNode sourceNode, out PlanNode targetNode)
        {
            sourceNode = null!;
            targetNode = null!;

            if (!e.Data.GetDataPresent(typeof(PlanNode)))
            {
                return false;
            }

            var source = e.Data.GetData(typeof(PlanNode)) as PlanNode;
            if (source == null)
            {
                return false;
            }

            var position = e.GetPosition(PlanTreeView);
            var target = GetNodeAtPosition(position);
            if (target == null)
            {
                return false;
            }

            sourceNode = source;
            targetNode = target;
            return true;
        }

        private bool CanReorder(PlanNode sourceNode, PlanNode targetNode)
        {
            if (ReferenceEquals(sourceNode, targetNode))
            {
                return false;
            }

            return ReferenceEquals(sourceNode.Parent, targetNode.Parent);
        }

        private PlanNode? GetNodeAtPosition(Point position)
        {
            var hit = PlanTreeView.InputHitTest(position) as DependencyObject;
            var targetItem = FindParentTreeViewItem(hit);
            return targetItem?.DataContext as PlanNode;
        }

        private static void ReorderInCollection(ObservableCollection<PlanNode> collection, PlanNode sourceNode, PlanNode targetNode)
        {
            var oldIndex = collection.IndexOf(sourceNode);
            var newIndex = collection.IndexOf(targetNode);
            if (oldIndex < 0 || newIndex < 0 || oldIndex == newIndex)
            {
                return;
            }

            collection.Move(oldIndex, newIndex);
        }

        private static TreeViewItem? FindParentTreeViewItem(DependencyObject? child)
        {
            while (child != null)
            {
                if (child is TreeViewItem item)
                {
                    return item;
                }

                child = VisualTreeHelper.GetParent(child);
            }

            return null;
        }
    }
}