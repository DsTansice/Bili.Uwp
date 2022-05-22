﻿// Copyright (c) Richasy. All rights reserved.

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using Bili.Lib.Interfaces;
using Bili.Models.Data.Community;
using Bili.Models.Data.Live;
using Bili.Models.Enums;
using Bili.Toolkit.Interfaces;
using Bili.ViewModels.Uwp.Base;
using Bili.ViewModels.Uwp.Core;
using ReactiveUI;
using Splat;
using Windows.UI.Core;

namespace Bili.ViewModels.Uwp.Live
{
    /// <summary>
    /// 直播分区详情页面视图模型.
    /// </summary>
    public sealed partial class LivePartitionDetailPageViewModel : InformationFlowViewModelBase
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="LivePartitionDetailPageViewModel"/> class.
        /// </summary>
        /// <param name="resourceToolkit">本地资源工具.</param>
        /// <param name="liveProvider">直播服务提供工具.</param>
        /// <param name="coreDispatcher">UI调度程序.</param>
        /// <param name="navigationViewModel">导航视图模型.</param>
        public LivePartitionDetailPageViewModel(
            IResourceToolkit resourceToolkit,
            ILiveProvider liveProvider,
            CoreDispatcher coreDispatcher,
            NavigationViewModel navigationViewModel)
            : base(coreDispatcher)
        {
            _resourceToolkit = resourceToolkit;
            _liveProvider = liveProvider;
            _navigationViewModel = navigationViewModel;
            _caches = new Dictionary<LiveTag, IEnumerable<LiveInformation>>();

            Tags = new ObservableCollection<LiveTag>();

            SelectTagCommand = ReactiveCommand.CreateFromTask<LiveTag>(SelectTagAsync, outputScheduler: RxApp.MainThreadScheduler);
            SeeAllPartitionsCommand = ReactiveCommand.Create(SeeAllPartitions, outputScheduler: RxApp.MainThreadScheduler);
        }

        /// <summary>
        /// 设置初始分区.
        /// </summary>
        /// <param name="partition">分区信息.</param>
        public void SetPartition(Partition partition)
        {
            OriginPartition = partition;
            _caches.Clear();
            _liveProvider.ResetPartitionDetailState();
            Tags.Clear();
            CurrentTag = null;
            _totalCount = -1;
            IsEmpty = false;
            VideoCollection.Clear();
        }

        /// <inheritdoc/>
        protected override void BeforeReload()
            => _liveProvider.ResetPartitionDetailState();

        /// <inheritdoc/>
        protected override string FormatException(string errorMsg)
            => $"{_resourceToolkit.GetLocaleString(LanguageNames.RequestSubPartitionFailed)}\n{errorMsg}";

        /// <inheritdoc/>
        protected override async Task GetDataAsync()
        {
            if (_totalCount >= 0 && VideoCollection.Count >= _totalCount)
            {
                return;
            }

            IsEmpty = false;
            var sortType = CurrentTag == null
                ? string.Empty
                : CurrentTag.SortType;
            var data = await _liveProvider.GetLiveAreaDetailAsync(OriginPartition.Id, OriginPartition.ParentId, sortType);

            if (data.Tags?.Count() > 0 && Tags.Count == 0)
            {
                data.Tags.ToList().ForEach(p => Tags.Add(p));
                await Task.Delay(100);
                CurrentTag = Tags.First();
            }

            _totalCount = data.TotalCount;

            if (data.Lives?.Count() > 0)
            {
                foreach (var live in data.Lives)
                {
                    var liveVM = Splat.Locator.Current.GetService<LiveItemViewModel>();
                    liveVM.SetInformation(live);
                    VideoCollection.Add(liveVM);
                }

                var lives = VideoCollection
                        .OfType<LiveItemViewModel>()
                        .Select(p => p.Information)
                        .ToList();
                if (_caches.ContainsKey(CurrentTag))
                {
                    _caches[CurrentTag] = lives;
                }
                else
                {
                    _caches.Add(CurrentTag, lives);
                }
            }

            IsEmpty = VideoCollection.Count == 0;
        }

        private async Task SelectTagAsync(LiveTag tag)
        {
            await Task.Delay(100);
            VideoCollection.Clear();
            CurrentTag = tag;
            if (_caches.ContainsKey(tag))
            {
                var data = _caches[tag];
                foreach (var live in data)
                {
                    var liveVM = Splat.Locator.Current.GetService<LiveItemViewModel>();
                    liveVM.SetInformation(live);
                    VideoCollection.Add(liveVM);
                }
            }
            else
            {
                InitializeCommand.Execute().Subscribe();
            }
        }

        private void SeeAllPartitions()
            => _navigationViewModel.NavigateToSecondaryView(PageIds.LivePartition);
    }
}
