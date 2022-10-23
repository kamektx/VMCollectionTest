using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using VMCollectionTest.Collection;
using VMCollectionTest.Model;
using Livet.Commands;

namespace VMCollectionTest.ViewModel
{
    public class MainWindowVM : Livet.ViewModel
    {
        MainWindowModel _model;
        public VMCollection<ItemModel, ItemViewModel> Items { get; }

        private ItemViewModel? _selectedItem;
        public object? SelectedItem
        {
            get
            {
                return _selectedItem;
            }
            set
            {
                if (!(value is ItemViewModel vm))
                    return;
                _selectedItem = vm;
                RaisePropertyChanged();
                AddItemCommand.RaiseCanExecuteChanged();
            }
        }


        private ViewModelCommand? _AddItemCommand;

        public ViewModelCommand AddItemCommand
        {
            get
            {
                if (_AddItemCommand == null)
                {
                    _AddItemCommand = new ViewModelCommand(AddItem, CanAddItem);
                }
                return _AddItemCommand;
            }
        }

        public bool CanAddItem()
        {
            return _selectedItem != null;
        }

        public void AddItem()
        {
            if (_selectedItem == null)
                return;
            var index = Items.IndexOf(_selectedItem);
            Items.Insert(index + 1, new ItemViewModel());
            
        }


        public MainWindowVM()
        {
            _model = new MainWindowModel();
            Items = new VMCollection<ItemModel, ItemViewModel>(
                _model.Items,
                (model) => new ItemViewModel(model),
                Livet.DispatcherHelper.UIDispatcher);
        }
    }
}
