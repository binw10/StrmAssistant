define(['connectionManager', 'globalize', 'loading', 'toast', 'confirm', 'dialog'], function (connectionManager, globalize, loading, toast, confirm, dialog) {

    return {
        copy: function (libraryId) {
            loading.show();

            let apiClient = connectionManager.currentApiClient();
            let copyApi = apiClient.getUrl('Library/VirtualFolders/Copy');

            apiClient.ajax({
                type: "POST",
                url: copyApi,
                data: JSON.stringify({ Id: libraryId }),
                contentType: "application/json"
            }).finally(() => {
                loading.hide();
                const locale = globalize.getCurrentLocale().toLowerCase();
                const confirmMessage = (locale === 'zh-cn') ? '\u590d\u5236\u5a92\u4f53\u5e93\u6210\u529f' : 
                    (['zh-hk', 'zh-tw'].includes(locale) ? '\u8907\u88fd\u5a92\u9ad4\u5eab\u6210\u529f' : 'Copy Library Success');
                toast(confirmMessage);
                const itemsContainer = document.querySelector('.view-librarysetup-library .itemsContainer, .view-librarysetup-librarysetup .itemsContainer');
                if (itemsContainer) {
                    itemsContainer.notifyRefreshNeeded(true);
                }
            });
        },

        remove: function (libraryId, libraryName) {
            confirm({
                text: globalize.translate('MessageAreYouSureYouWishToRemoveLibrary').replace('{0}', libraryName),
                title: globalize.translate('HeaderRemoveLibrary'),
                confirmText: globalize.translate('Remove'),
                primary: 'cancel'
            })
            .then(function() {
                loading.show();

                let apiClient = connectionManager.currentApiClient();
                let deleteApi = apiClient.getUrl('Library/VirtualFolders/Delete');

                apiClient.ajax({
                    type: "POST",
                    url: deleteApi + "?refreshLibrary=false&id=" + libraryId,
                    data: {},
                    contentType: "application/json"
                }).finally(() => {
                    loading.hide();
                    const locale = globalize.getCurrentLocale().toLowerCase();
                    const confirmMessage = (locale === 'zh-cn') ? '\u5408\u96c6\u5220\u9664\u6210\u529f' : 
                        (['zh-hk', 'zh-tw'].includes(locale) ? '\u5408\u96C6\u5236\u9662\u6210\u529F' : 'Delete Collections Success');
                    toast(confirmMessage);
                    const itemsContainer = document.querySelector('.view-librarysetup-library .itemsContainer, .view-librarysetup-librarysetup .itemsContainer');
                    if (itemsContainer) {
                        itemsContainer.notifyRefreshNeeded(true);
                    }
                });
            });
        },

        traverse: function (itemId) {
            loading.show();

            let apiClient = connectionManager.currentApiClient();
            let scanApi = apiClient.getUrl(`Items/${itemId}/Refresh`);
            let queryParams = {
                Recursive: true,
                ImageRefreshMode: 'Default',
                MetadataRefreshMode: 'Default',
                ReplaceAllImages: false,
                ReplaceAllMetadata: false
            };
            let queryString = new URLSearchParams(queryParams).toString();

            apiClient.ajax({
                type: "POST",
                url: `${scanApi}?${queryString}`,
                data: {},
                contentType: "application/json"
            }).finally(() => {
                loading.hide();
                const confirmMessage = globalize.translate('ScanningLibraryFilesDots');
                toast(confirmMessage);
            });
        },

        delver: function (itemId, itemName, itemType) {
            if (itemType === 'Movie') {
                confirm({
                    text: globalize.translate('ConfirmDeleteItems') + "\n\n" +
                            itemName + "\n\n" +
                            globalize.translate('AreYouSureToContinue'),
                    html: globalize.translate('ConfirmDeleteItems') +
                            '<p><div class="secondaryText">' + itemName + "</div></p>" +
                            '<p style="margin-bottom:0;">' + globalize.translate('AreYouSureToContinue') + "</p>",
                    title: globalize.translate('HeaderDeleteItem'),
                    confirmText: globalize.translate('Delete'),
                    primary: 'cancel',
                    centerText: !1
                })
                .then(function() {
                    deleteVersion(itemId);
                });
            } else {
                const locale = globalize.getCurrentLocale().toLowerCase();
                const deleteEpisode = (locale.startsWith('zh') || locale.startsWith('ja') || locale.startsWith('ko'))
                            ? globalize.translate('Delete') + globalize.translate('Episode')
                            : globalize.translate('Delete') + ' ' + globalize.translate('Episode');
                const deleteSeason = (locale.startsWith('zh') || locale.startsWith('ja') || locale.startsWith('ko'))
                            ? globalize.translate('Delete') + globalize.translate('Season')
                            : globalize.translate('Delete') + ' ' + globalize.translate('Season');
                dialog({
                    text: globalize.translate('ConfirmDeleteItems') + "\n\n" +
                            itemName + "\n\n" +
                            globalize.translate('AreYouSureToContinue'),
                    html: globalize.translate('ConfirmDeleteItems') +
                            '<p><div class="secondaryText">' + itemName + "</div></p>" +
                            '<p style="margin-bottom:0;">' + globalize.translate('AreYouSureToContinue') + "</p>",
                    title: globalize.translate('HeaderDeleteItem'),
                    buttons: [
                        { name: globalize.translate("Cancel"), id: "cancel", type: "submit" },
                        { name: deleteEpisode, id: "deleteepisode", type: "cancel" },
                        { name: deleteSeason, id: "deleteseason", type: "cancel" }
                    ],
                    centerText: !1
                })
                .then(function(id) {
                    if (id === 'deleteepisode') {
                        deleteVersion(itemId);
                    } else if (id === 'deleteseason') {
                        deleteVersion(itemId, true);
                    }
                });
            }
            function deleteVersion(itemId, deleteParent = false) {
                loading.show();
                let apiClient = connectionManager.currentApiClient();
                let deleteApi = apiClient.getUrl(`Items/${itemId}/DeleteVersion${deleteParent ? `?DeleteParent=true` : ''}`);
                apiClient.ajax({
                    type: "POST",
                    url: deleteApi,
                    data: {},
                    contentType: "application/json"
                }).finally(() => {
                    loading.hide();
                    const locale = globalize.getCurrentLocale().toLowerCase();
                    const confirmMessage = (locale === 'zh-cn') ? '\u5220\u9664\u7248\u672C\u6210\u529F' : 
                        (['zh-hk', 'zh-tw'].includes(locale) ? '\u524A\u9664\u7248\u672C\u6210\u529F' : 'Delete Version Success');
                    toast(confirmMessage);
                });
            }
        },

        lock: function (itemId, lockData) {
            let apiClient = connectionManager.currentApiClient();
            let lockApi = apiClient.getUrl(`Items/${itemId}/Lock`);
            let queryParams = {
                LockData: lockData
            };
            let queryString = new URLSearchParams(queryParams).toString();

            apiClient.ajax({
                type: "POST",
                url: `${lockApi}?${queryString}`,
                data: {},
                contentType: "application/json"
            });
        },

        clear_intro: function (itemIds) {
            const locale = globalize.getCurrentLocale().toLowerCase();
            const commandName = locale === 'zh-cn' ? '\u6E05\u9664\u7247\u5934\u6807\u8BB0' :
                    (['zh-hk', 'zh-tw'].includes(locale) ? '\u6E05\u9664\u7247\u982D\u6A19\u8A18' : 'Clear Intro Markers');
            batchClear(itemIds, 'ClearIntro', commandName);
        },

        clear_chapter: function (itemIds) {
            const locale = globalize.getCurrentLocale().toLowerCase();
            const commandName = locale === 'zh-cn' ? '\u6E05\u9664\u7AE0\u8282\u56FE' :
                    (['zh-hk', 'zh-tw'].includes(locale) ? '\u6E05\u9664\u7AE0\u7BC0\u5716' : 'Clear Chapter Images');
            batchClear(itemIds, 'ClearChapterImages', commandName);
        },

        clear_mediainfo: function (itemIds) {
            const locale = globalize.getCurrentLocale().toLowerCase();
            const commandName = locale === 'zh-cn' ? '\u6E05\u9664\u5A92\u4F53\u4FE1\u606F' :
                    (['zh-hk', 'zh-tw'].includes(locale) ? '\u6E05\u9664\u5A92\u9AD4\u8CC7\u8A0A' : 'Clear Media Info');
            batchClear(itemIds, 'ClearMediaInfo', commandName);
        },

        badges: (function () {
            let initialized = false;
            let enabled = null;
            let scanTimer = null;

            function apiClient() {
                return connectionManager.currentApiClient();
            }

            function checkEnabled() {
                if (enabled !== null) return Promise.resolve(enabled);
                return apiClient().getJSON(apiClient().getUrl('Douban/Ratings')).then(function (res) {
                    enabled = !!(res && (res.Enabled !== undefined ? res.Enabled : res.enabled));
                    return enabled;
                }).catch(function () {
                    enabled = false;
                    return false;
                });
            }

            function fetchRatings(ids) {
                return apiClient().getJSON(apiClient().getUrl('Douban/Ratings', { Ids: ids.join(',') }))
                    .then(function (res) { return (res && (res.Ratings || res.ratings)) || []; })
                    .catch(function () { return []; });
            }

            function ratingOf(info) {
                return info && (info.Rating !== undefined ? info.Rating : info.rating);
            }

            function idOf(info) {
                return info && (info.ItemId !== undefined ? info.ItemId : info.itemId);
            }

            function makePill(info, floating) {
                const pill = document.createElement('div');
                pill.className = 'douban-badge';
                pill.textContent = '\u8C46\u74E3 ' + Number(ratingOf(info)).toFixed(1);
                let style = 'z-index:5;background:rgba(0,0,0,.72);color:#ffac2d;font-size:12px;' +
                    'line-height:16px;padding:0 6px;border-radius:8px;pointer-events:none;white-space:nowrap;';
                style += floating ? 'position:absolute;top:4px;right:4px;' : 'display:inline-block;vertical-align:middle;';
                pill.setAttribute('style', style);
                return pill;
            }

            function decorateCard(card, info) {
                if (card.getAttribute('data-douban-badge')) return;
                card.setAttribute('data-douban-badge', '1');
                const host = card.querySelector('.cardScalable') || card.querySelector('.cardBox') || card;
                if (!host) return;
                if (host.style.position !== 'absolute' && !host.style.position) host.style.position = 'relative';
                host.appendChild(makePill(info, true));
            }

            function decorateDetail() {
                const page = document.querySelector('.itemDetailPage');
                if (!page || page.getAttribute('data-douban-badge')) return;

                const match = (location.hash || '').match(/[?&]id=(\d+)/);
                if (!match) return;

                page.setAttribute('data-douban-badge', '1');

                fetchRatings([match[1]]).then(function (list) {
                    const info = list && list[0];
                    if (!info || !ratingOf(info)) return;
                    const host = page.querySelector('.itemMiscInfo') || page.querySelector('.mainDetailButtons');
                    if (!host) return;
                    host.appendChild(makePill(info, false));
                }).catch(function () {});
            }

            function scan() {
                try {
                    decorateDetail();

                    const cards = Array.prototype.slice.call(
                        document.querySelectorAll('.card[data-id]:not([data-douban-badge])'));
                    if (!cards.length) return;

                    const seen = {};
                    const ids = [];
                    for (const card of cards) {
                        const id = card.getAttribute('data-id');
                        if (!id || seen[id]) continue;
                        seen[id] = true;
                        ids.push(id);
                        if (ids.length >= 60) break;
                    }
                    if (!ids.length) return;

                    fetchRatings(ids).then(function (list) {
                        const byId = {};
                        (list || []).forEach(function (info) { byId[idOf(info)] = info; });
                        cards.forEach(function (card) {
                            const info = byId[card.getAttribute('data-id')];
                            if (info && ratingOf(info)) decorateCard(card, info);
                            else card.setAttribute('data-douban-badge', '1');
                        });
                    }).catch(function () {});
                } catch (e) {
                    // ignore
                }
            }

            function scheduleScan() {
                if (scanTimer) clearTimeout(scanTimer);
                scanTimer = setTimeout(scan, 600);
            }

            function init() {
                if (initialized) return;
                initialized = true;

                checkEnabled().then(function (on) {
                    if (!on) return;
                    scan();
                    if (window.MutationObserver) {
                        new MutationObserver(scheduleScan).observe(document.body,
                            { childList: true, subtree: true });
                    }
                });
            }

            return { init: init };
        })()
    };

    function batchClear(itemIds, endpoint, commandName) {
        const ids = Array.isArray(itemIds) ? itemIds : [itemIds];
        if (!ids.length) return;

        confirm({
            text: globalize.translate('AreYouSureToContinue'),
            title: commandName,
            confirmText: globalize.translate('Clear'),
            primary: 'cancel'
        })
        .then(function() {
            loading.show();
            let apiClient = connectionManager.currentApiClient();
            return Promise.all(ids.map(id => apiClient.ajax({
                type: "POST",
                url: apiClient.getUrl(`Items/${id}/${endpoint}`),
                data: {},
                contentType: "application/json"
            })));
        })
        .then(function() {
            loading.hide();
            const locale = globalize.getCurrentLocale().toLowerCase();
            const confirmMessage = (locale === 'zh-cn') ? commandName + '\u6210\u529F' :
                (['zh-hk', 'zh-tw'].includes(locale) ? commandName + '\u6210\u529F' : commandName + ' Success');
            toast(confirmMessage);
        })
        .catch(function() {
            loading.hide();
        });
    }
});
