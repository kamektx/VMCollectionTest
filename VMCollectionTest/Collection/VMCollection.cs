using Livet;
using Livet.EventListeners;
using Livet.StatefulModel;
using Microsoft.VisualBasic;
using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Documents;
using System.Windows.Threading;

namespace VMCollectionTest
{
    /// <summary>
    /// VMCollectionで用いられるViewModel要素はModelへのゲッターを公開する必要があります。<br/>
    /// Dispose時にModelへの参照は維持したままにしてください。<br/>
    /// デフォルトコンストラクタではModelはnullに設定してください。
    /// </summary>
    public interface IModelProperty
    {
        object? Model { get; }
    }
}

namespace VMCollectionTest.Collection
{
    public interface IObservableCollection<TViewModel> : IList<TViewModel>, ICollection, INotifyCollectionChanged, INotifyPropertyChanged
    {
        void Move(int oldIndex, int newIndex);
    }
    public interface IDisposed : IDisposable
    {
        event EventHandler? Disposed;
    }

    public class VMCollection<TModel, TViewModel> : IList<TViewModel>, ICollection, INotifyCollectionChanged, INotifyPropertyChanged, IReadOnlyList<TViewModel>, IDisposable, IDisposed, IObservableCollection<TViewModel>
        where TModel : new()
        where TViewModel : class?, IModelProperty, new()
    {
        private readonly List<TViewModel> _vmList;
        private IList<TModel>? _modelCollectionRef;
        private Dispatcher _dispatcher;
        private readonly LivetCompositeDisposable _listeners = new LivetCompositeDisposable();
        private int _disposed = 0;
        private bool _isTViewModelDisposable;
        private Func<TViewModel, TModel> _viewModelToModel;

        /// <summary>
        ///     コンストラクタ
        /// </summary>
        /// <param name="modelCollection">TModel型を要素に持つ監視対象コレクション</param>
        /// <param name="modelToViewModel">TModel型のインスタンスをTViewModel型のインスタンスに変換するFunc</param>
        /// <param name="viewModelToModel">TViewModel型のインスタンスをTModel型のインスタンスに変換するFunc。よく分からなければ3引数のコンストラクタを使用してください</param>
        /// <param name="dispatcher">UIDispatcher(通常はDispatcherHelper.UIDispatcher)</param>
        public VMCollection(INotifyCollectionChanged modelCollection, Func<TModel, TViewModel> modelToViewModel, Func<TViewModel, TModel> viewModelToModel, Dispatcher dispatcher)
        {
            if (modelCollection == null) throw new ArgumentNullException(nameof(modelCollection));
            if (modelToViewModel == null) throw new ArgumentNullException(nameof(modelToViewModel));
            if (viewModelToModel == null) throw new ArgumentNullException(nameof(viewModelToModel));
            if (!(modelCollection is IList<TModel> modelCollectionAsList)) throw new ArgumentException("collectionはIList<T>を実装している必要があります");
            if (!(modelCollection is INotifyPropertyChanged)) throw new ArgumentException("collectionはINotifyPropertyChangedを実装している必要があります");
            if (dispatcher == null) throw new ArgumentNullException(nameof(dispatcher));

            _dispatcher = dispatcher;
            _modelCollectionRef = modelCollectionAsList;
            _isTViewModelDisposable = typeof(IDisposable).IsAssignableFrom(typeof(TViewModel));
            _viewModelToModel = viewModelToModel;


            _vmList = new List<TViewModel>();
            foreach (var model in _modelCollectionRef)
            {
                var vm = modelToViewModel(model);
                _vmList.Add(vm);
            }


            if (modelCollection is IDisposed disposableCollection)
            {
                disposableCollection.Disposed += (s, e) =>
                {
                    Dispose();
                };
            }

            CollectionChangedDispatcherPriority = DispatcherPriority.Normal;

            _listeners.Add(new PropertyChangedEventListener(
                (INotifyPropertyChanged)_modelCollectionRef,
                (sender, e) =>
                {
                    if (!Dispatcher.CheckAccess())
                    {
                        Dispatcher.Invoke(CollectionChangedDispatcherPriority, () => OnPropertyChanged(e));
                    }
                    else
                        OnPropertyChanged(e);
                }));

            _listeners.Add(new CollectionChangedEventListener(
                (INotifyCollectionChanged)_modelCollectionRef,
                (sender, e) =>
                {
                    if (e == null) throw new ArgumentNullException(nameof(e));

                    NotifyCollectionChangedEventArgs? newArgs = null;

                    switch (e.Action)
                    {
                        case NotifyCollectionChangedAction.Add:
                            if (!(e.NewItems?[0] is TModel addedModel))
                            {
                                throw new ArgumentException("NotifyCollectionChangedEventArgs.NewItemsの要素型が正しくありません", nameof(e));
                            }
                            var addedVM = modelToViewModel(addedModel);
                            _vmList.Insert(e.NewStartingIndex, addedVM);
                            newArgs = new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Add, addedVM, e.NewStartingIndex);
                            break;
                        case NotifyCollectionChangedAction.Move:
                            TViewModel movedVM = _vmList[e.OldStartingIndex];
                            _vmList.RemoveAt(e.OldStartingIndex);
                            _vmList.Insert(e.NewStartingIndex, movedVM);
                            newArgs = new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Move, movedVM, e.NewStartingIndex, e.OldStartingIndex);
                            break;
                        case NotifyCollectionChangedAction.Remove:
                            TViewModel removedVM = _vmList[e.OldStartingIndex];
                            if (_isTViewModelDisposable)
                                ((IDisposable)removedVM).Dispose();
                            _vmList.RemoveAt(e.OldStartingIndex);
                            newArgs = new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Remove, removedVM, e.OldStartingIndex);
                            break;
                        case NotifyCollectionChangedAction.Replace:
                            if (!(e.NewItems?[0] is TModel replacingModel))
                            {
                                throw new ArgumentException("NotifyCollectionChangedEventArgs.NewItemsの要素型が正しくありません", nameof(e));
                            }
                            var replacingVM = modelToViewModel(replacingModel);
                            var replacedVM = _vmList[e.NewStartingIndex];
                            _vmList[e.NewStartingIndex] = replacingVM;
                            newArgs = new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Replace, replacingVM, replacedVM, e.NewStartingIndex);
                            break;
                        case NotifyCollectionChangedAction.Reset:
                            if (_isTViewModelDisposable)
                            {
                                foreach (var item in _vmList)
                                {
                                    ((IDisposable)item).Dispose();
                                }
                            }
                            _vmList.Clear();
                            newArgs = new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset);
                            break;
                        default:
                            throw new ArgumentException();
                    }
                    if (!Dispatcher.CheckAccess())
                        Dispatcher.Invoke(CollectionChangedDispatcherPriority, () => OnCollectionChanged(newArgs));
                    else
                        OnCollectionChanged(newArgs);
                }
            ));
        }

        /// <summary>
        ///     コンストラクタ
        /// </summary>
        /// <param name="modelCollection">TModel型を要素に持つ監視対象コレクション</param>
        /// <param name="modelToViewModel">TModel型のインスタンスをTViewModel型のインスタンスに変換するFunc</param>
        /// <param name="dispatcher">UIDispatcher(通常はDispatcherHelper.UIDispatcher)</param>
        public VMCollection(INotifyCollectionChanged modelCollection, Func<TModel, TViewModel> modelToViewModel, Dispatcher dispatcher)
            : this(modelCollection, modelToViewModel, (vm) =>
            {
                if (vm?.Model == null)
                {
                    return new TModel();
                }
                return (TModel)vm.Model;
            }, dispatcher)
        { }

        /// <summary>
        ///     このコレクションに関連付けられたDispatcherを取得、または設定します。
        /// </summary>
        public Dispatcher Dispatcher
        {
            get { return _dispatcher; }
            set { _dispatcher = value ?? throw new ArgumentNullException(nameof(value)); }
        }

        /// <summary>
        ///     コレクション変更通知時のDispatcherPriorityを指定、または取得します。
        /// </summary>
        public DispatcherPriority CollectionChangedDispatcherPriority { get; set; }

        /// <summary>
        ///     全体を互換性のある1次元の配列にコピーします。コピー操作は、コピー先の配列の指定したインデックスから始まります。
        /// </summary>
        /// <param name="array">コピー先の配列</param>
        /// <param name="index">コピー先の配列のどこからコピー操作をするかのインデックス</param>
        public void CopyTo(Array array, int index)
        {
            CopyTo(array.Cast<TViewModel>().ToArray(), index);
        }

        /// <summary>
        ///     このコレクションがスレッドセーフであるかどうかを取得します。
        /// </summary>
        public bool IsSynchronized => false;

        /// <summary>
        ///     このコレクションへのスレッドセーフなアクセスに使用できる同期オブジェクトを返します。
        /// </summary>
        public object SyncRoot { get; } = new object();

        /// <summary>
        ///     指定したオブジェクトを検索し、最初に見つかった位置の 0 から始まるインデックスを返します。
        /// </summary>
        /// <param name="item">検索するオブジェクト</param>
        /// <returns>最初に見つかった位置のインデックス</returns>
        public int IndexOf(TViewModel item)
        {
            ThrowExceptionIfDisposed();
            return _vmList.IndexOf(item);
        }

        /// <summary>
        ///     指定したインデックスの位置に要素を挿入します。
        /// </summary>
        /// <param name="index">指定するインデックス</param>
        /// <param name="item">挿入するオブジェクト</param>
        public void Insert(int index, TViewModel item)
        {
            ThrowExceptionIfDisposed();
            var model = _viewModelToModel(item);
            _modelCollectionRef?.Insert(index, model);
        }

        /// <summary>
        ///     指定したインデックスにある要素を削除します。
        /// </summary>
        /// <param name="index">指定するインデックス</param>
        public void RemoveAt(int index)
        {
            ThrowExceptionIfDisposed();
            _modelCollectionRef?.RemoveAt(index);
        }

        public TViewModel this[int index]
        {
            get => _vmList[index];
            set
            {
                if (_modelCollectionRef == null) return;
                var model = _viewModelToModel(value);
                _modelCollectionRef[index] = model;
                _vmList[index] = value;
            }
        }

        /// <summary>
        ///     末尾にオブジェクトを追加します。
        /// </summary>
        /// <param name="item">追加するオブジェクト</param>
        public void Add(TViewModel item)
        {
            ThrowExceptionIfDisposed();
            var model = _viewModelToModel(item);
            _modelCollectionRef?.Add(model);
        }

        /// <summary>
        ///     すべての要素を削除します。
        /// </summary>
        public void Clear()
        {
            ThrowExceptionIfDisposed();
            _modelCollectionRef?.Clear();
        }

        /// <summary>
        ///     ある要素がこのコレクションに含まれているかどうかを判断します。
        /// </summary>
        /// <param name="item">コレクションに含まれているか判断したい要素</param>
        /// <returns>このコレクションに含まれているかどうか</returns>
        public bool Contains(TViewModel item)
        {
            ThrowExceptionIfDisposed();
            return _vmList.Contains(item);
        }

        /// <summary>
        ///     全体を互換性のある1次元の配列にコピーします。コピー操作は、コピー先の配列の指定したインデックスから始まります。
        /// </summary>
        /// <param name="array">コピー先の配列</param>
        /// <param name="arrayIndex">コピー先の配列のどこからコピー操作をするかのインデックス</param>
        public void CopyTo(TViewModel[] array, int arrayIndex)
        {
            ThrowExceptionIfDisposed();
            _vmList.CopyTo(array, arrayIndex);
        }

        /// <summary>
        ///     実際に格納されている要素の数を取得します。
        /// </summary>
        public int Count
        {
            get { return _vmList.Count; }
        }

        /// <summary>
        ///     このコレクションが読み取り専用かどうかを取得します。
        /// </summary>
        public bool IsReadOnly => false;

        /// <summary>
        ///     最初に見つかった特定のオブジェクトを削除します。
        /// </summary>
        /// <param name="item">削除したいオブジェクト</param>
        /// <returns>削除できたかどうか</returns>
        public bool Remove(TViewModel item)
        {
            ThrowExceptionIfDisposed();
            var model = _viewModelToModel(item);
            return _modelCollectionRef?.Remove(model) ?? false;
        }

        /// <summary>
        ///     反復処理するための列挙子を返します。
        /// </summary>
        /// <returns>列挙子</returns>
        public IEnumerator<TViewModel> GetEnumerator()
        {
            ThrowExceptionIfDisposed();
            return _vmList.GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            ThrowExceptionIfDisposed();
            return _vmList.GetEnumerator();
        }

        /// <summary>
        ///     プロパティが変更された際に発生するイベントです。
        /// </summary>
        public event NotifyCollectionChangedEventHandler? CollectionChanged;

        /// <summary>
        ///     コレクションが変更された際に発生するイベントです。
        /// </summary>
        public event PropertyChangedEventHandler? PropertyChanged;

        /// <summary>
        ///     Disposeされた際に呼ばれるイベントです。
        /// </summary>
        public event EventHandler? Disposed;

        /// <summary>
        ///     指定されたインデックスの要素を指定されたインデックスに移動します。
        /// </summary>
        /// <param name="oldIndex">移動したい要素のインデックス</param>
        /// <param name="newIndex">移動先のインデックス</param>
        public void Move(int oldIndex, int newIndex)
        {
            ThrowExceptionIfDisposed();
            switch(_modelCollectionRef)
            {
                case ObservableCollection<TModel> collection:
                    collection.Move(oldIndex, newIndex);
                    break;
                case ObservableSynchronizedCollection<TModel> collection:
                    collection.Move(oldIndex, newIndex);
                    break;
                case IObservableCollection<TModel> collection:
                    collection.Move(oldIndex, newIndex);
                    break;
                default:
                    Debug.WriteLine("modelCollectionが知らない型だったのでMoveできませんでした。");
                    break;
            }
        }

        protected void OnCollectionChanged(NotifyCollectionChangedEventArgs args)
        {
            ThrowExceptionIfDisposed();
            var threadSafeHandler = Interlocked.CompareExchange(ref CollectionChanged, null, null);
            threadSafeHandler?.Invoke(this, args);
        }
        protected void OnPropertyChanged(string propertyName)
        {
            ThrowExceptionIfDisposed();
            var threadSafeHandler = Interlocked.CompareExchange(ref PropertyChanged, null, null);
            threadSafeHandler?.Invoke(this, EventArgsFactory.GetPropertyChangedEventArgs(propertyName));
        }
        protected void OnPropertyChanged(PropertyChangedEventArgs args)
        {
            ThrowExceptionIfDisposed();
            var threadSafeHandler = Interlocked.CompareExchange(ref PropertyChanged, null, null);
            threadSafeHandler?.Invoke(this, args);
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }
        ~VMCollection()
        {
            Dispose(false);
        }
        protected virtual void Dispose(bool disposing)
        {
            if (Interlocked.CompareExchange(ref _disposed, 1, 0) == 1) return;

            if (disposing)
            {
                _listeners.Dispose();

                if (_isTViewModelDisposable)
                {
                    foreach (var i in _vmList)
                        ((IDisposable)i).Dispose();
                }

                _vmList.Clear();
                _modelCollectionRef = null;
                Disposed?.Invoke(this, EventArgs.Empty);
                CollectionChanged = null;
                PropertyChanged = null;
                Disposed = null;
            }
        }
        protected void ThrowExceptionIfDisposed()
        {
            if (_disposed == 1) throw new ObjectDisposedException("VMCollection");
        }

        private static class EventArgsFactory
        {
            private static readonly ConcurrentDictionary<string, PropertyChangedEventArgs>
                PropertyChangedEventArgsDictionary = new ConcurrentDictionary<string, PropertyChangedEventArgs>();

            public static PropertyChangedEventArgs GetPropertyChangedEventArgs(string propertyName)
            {
                return PropertyChangedEventArgsDictionary.GetOrAdd(propertyName ?? string.Empty,
                    name => new PropertyChangedEventArgs(name));
            }
        }
    }
}
