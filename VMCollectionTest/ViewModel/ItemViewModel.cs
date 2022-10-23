using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using VMCollectionTest.Collection;
using VMCollectionTest.Model;

namespace VMCollectionTest.ViewModel
{
    public class ItemViewModel : Livet.ViewModel, IModelProperty
    {
        private ItemModel? _model;
        public object? Model => _model;

        public string Name
        {
            get
            {
                if (_model == null) return "Model is null.";
                return _model.Name + "000";
            }
            set
            {
                if (_model == null) return;
                if (value.Length > 3)
                {
                    _model.Name = value.Substring(0, value.Length - 3);
                    return;
                }
                _model.Name = string.Empty;
                RaisePropertyChanged();
            }
        }

        public ItemViewModel()
        { }

        public ItemViewModel(ItemModel model)
        {
            _model = model;
        }
    }
}
