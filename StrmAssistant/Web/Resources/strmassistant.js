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

            function makeDoubanMark() {
                // 自绘「豆瓣」标识：绿底圆角方块 + 白「豆」字。
                // 纯 DOM/CSS，不依赖任何外部图片或网络资源。
                const mark = document.createElement('span');
                mark.className = 'douban-mark';
                mark.setAttribute('style',
                    'display:inline-block;width:12px;height:12px;line-height:12px;border-radius:3px;' +
                    'background:#2E963B;color:#fff;font-size:9px;font-weight:700;text-align:center;' +
                    'margin-right:4px;vertical-align:1px;font-family:"PingFang SC","Microsoft YaHei",sans-serif;');
                mark.textContent = '\u8C46';
                return mark;
            }

            function makePill(info, floating) {
                const pill = document.createElement('div');
                pill.className = 'douban-badge';
                pill.appendChild(makeDoubanMark());
                pill.appendChild(document.createTextNode(Number(ratingOf(info)).toFixed(1)));
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
        })(),

        versions: (function () {
            let initialized = false;
            let enabled = null;
            let scanTimer = null;
            let currentId = null;
            let currentRes = null;

            function apiClient() {
                return connectionManager.currentApiClient();
            }

            function checkEnabled() {
                if (enabled !== null) return Promise.resolve(enabled);
                return apiClient().getJSON(apiClient().getUrl('MultiVersion/Info')).then(function (res) {
                    enabled = !!(res && (res.Enabled !== undefined ? res.Enabled : res.enabled));
                    return enabled;
                }).catch(function () {
                    enabled = false;
                    return false;
                });
            }

            // 与后端 MultiVersionService.QualityScore/GetHdrLabel 保持一致的档位顺序（前=最优）。
            // 注意：顺序必须含 "HDR Vivid"（对应后端 350 分），且未识别的取值应归入最低档，
            // 不能因为 indexOf 返回 -1 被 Math.max(0, ...) 误判为最高档。
            function rankOf(list, value) {
                if (!value) return list.length;
                const idx = list.indexOf(value);
                return idx < 0 ? list.length : idx;
            }

            function scoreOf(info) {
                if (!info) return -1;
                const labels = ['8K', '4K', '1440p', '1080p', '720p', 'SD'];
                const hdrs = ['DV', 'HDR10+', 'HDR Vivid', 'HDR'];
                const label = info.Label || info.label;
                const hdr = info.Hdr || info.hdr;
                const lr = rankOf(labels, label);
                const hr = rankOf(hdrs, hdr);
                const bitrate = (info.Bitrate || info.bitrate || 0) / 1000000;
                return (labels.length - lr) * 10000 + (hdrs.length + 1 - hr) * 100 + Math.min(bitrate, 99);
            }

            function makeChip(info) {
                const chip = document.createElement('span');
                chip.className = 'mediaInfoItem mv-quality-badge';
                const label = info.Label || info.label;
                const hdr = info.Hdr || info.hdr;
                chip.textContent = label + (hdr ? ' \u00b7 ' + hdr : '');
                chip.setAttribute('style', 'color:#7ee787;background:rgba(46,160,67,.18);font-weight:600;');
                return chip;
            }

            function decorateSources(page, res) {
                const container = page.querySelector('.mediaSources');
                if (!container || container.getAttribute('data-mv-done')) return;

                const sources = (res && (res.Sources || res.sources)) || [];
                container.setAttribute('data-mv-done', '1');

                if (!sources.length) return;

                const rows = Array.prototype.slice.call(container.querySelectorAll('.mediaSource'));
                const entries = rows.map(function (row, i) {
                    return {
                        row: row,
                        wrapper: row.parentElement === container ? row : row.parentElement,
                        info: sources[i]
                    };
                });

                entries.forEach(function (e) {
                    if (!e.info || e.row.querySelector('.mv-quality-badge')) return;
                    const items = e.row.querySelector('.mediaInfoItems');
                    const label = e.info.Label || e.info.label;
                    if (!items || !label) return;
                    items.appendChild(makeChip(e.info));
                });

                const sorted = entries.slice().sort(function (a, b) { return scoreOf(b.info) - scoreOf(a.info); });
                const changed = entries.some(function (e, i) { return sorted[i].row !== e.row; });
                if (changed) sorted.forEach(function (e) { container.appendChild(e.wrapper); });
            }

            function decorateCards(page) {
                const cards = Array.prototype.slice.call(
                    page.querySelectorAll('.additionalPartsSection .card[data-id]:not([data-mv-card])'));
                if (!cards.length) return;

                const ids = [];
                const seen = {};
                cards.forEach(function (c) {
                    const id = c.getAttribute('data-id');
                    if (id && !seen[id]) { seen[id] = true; ids.push(id); }
                });
                if (!ids.length) return;

                apiClient().getJSON(apiClient().getUrl('MultiVersion/Info', { Ids: ids.join(',') })).then(function (res) {
                    const byId = {};
                    ((res && (res.Items || res.items)) || []).forEach(function (info) {
                        byId[info.ItemId !== undefined ? info.ItemId : info.itemId] = info;
                    });
                    cards.forEach(function (card) {
                        card.setAttribute('data-mv-card', '1');
                        const info = byId[card.getAttribute('data-id')];
                        const label = info && (info.Label || info.label);
                        if (!label) return;
                        const host = card.querySelector('.cardScalable') || card.querySelector('.cardBox') || card;
                        if (!host) return;
                        if (host.style.position !== 'absolute' && !host.style.position) host.style.position = 'relative';
                        const hdr = info.Hdr || info.hdr;
                        const pill = document.createElement('div');
                        pill.className = 'mv-quality-pill';
                        pill.textContent = label + (hdr ? ' \u00b7 ' + hdr : '');
                        pill.setAttribute('style', 'position:absolute;top:4px;left:4px;z-index:5;background:rgba(46,160,67,.85);' +
                            'color:#fff;font-size:11px;line-height:15px;padding:0 5px;border-radius:6px;pointer-events:none;white-space:nowrap;');
                        host.appendChild(pill);
                    });
                }).catch(function () {});
            }

            function scan() {
                try {
                    const page = document.querySelector('.itemDetailPage');
                    if (!page) return;

                    const match = (location.hash || '').match(/[?&]id=(\d+)/);
                    if (!match) return;

                    decorateCards(page);

                    const container = page.querySelector('.mediaSources');
                    if (!container || container.getAttribute('data-mv-done')) return;

                    if (currentId !== match[1]) {
                        currentId = match[1];
                        currentRes = null;

                        apiClient().getJSON(apiClient().getUrl('MultiVersion/Info', { Id: match[1] })).then(function (res) {
                            if (res && (res.Enabled === false || res.enabled === false)) {
                                enabled = false;
                                return;
                            }
                            currentRes = res;
                            if (currentRes) decorateSources(page, currentRes);
                        }).catch(function () { currentId = null; });
                    } else if (currentRes) {
                        decorateSources(page, currentRes);
                    }
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
        })(),

        ui: (function () {
            let initialized = false;
            let scanTimer = null;
            let countCache = {};
            let logReversed = false;

            function cfg() { return window.__saUI || {}; }

            function apiClient() { return connectionManager.currentApiClient(); }

            function scheduleScan() {
                if (scanTimer) clearTimeout(scanTimer);
                scanTimer = setTimeout(scan, 500);
            }

            function anyEnabled() {
                const c = cfg();
                return c.replaceUnplayedWithTotal || c.showLibraryItemCount ||
                    c.reverseLogLines || c.blockSearchRecommend || c.showMediaInfo;
            }

            // ShowMediaInfo（PRO 同构）：在搜索结果页，按当前搜索 tab 类型为结果项追加显示字段
            // （评分 / 容器格式等），使搜索结果直接呈现媒体信息。包裹 ItemsView.getViewSettingDefaults。
            let mediaInfoPatched = false;

            function patchSearchMediaInfo() {
                if (mediaInfoPatched || !cfg().showMediaInfo) return;

                try {
                    require(['components/items/itemsview'], function (responses) {
                        const ItemsView = responses && responses[0];
                        const proto = ItemsView && ItemsView.prototype;
                        if (!proto || !proto.getViewSettingDefaults || proto.__saMediaInfoPatched) return;

                        const original = proto.getViewSettingDefaults;
                        proto.getViewSettingDefaults = function () {
                            let result;
                            try {
                                result = original.apply(this, arguments);
                                const params = this.params;
                                if (!params || params.type !== 'search') return result;

                                const view = this.view;
                                const tabEl = view && view.querySelector(
                                    '.emby-searchable-tab-button.emby-tab-button-active');
                                const tab = tabEl ? tabEl.getAttribute('data-searchtype') : null;
                                if (!tab) return result;

                                if (!Array.isArray(result.fields)) result.fields = [];
                                const fields = result.fields;

                                if (tab === 'Movie' || tab === 'Series' || tab === 'Season') {
                                    if (fields.indexOf('CommunityRating') < 0) {
                                        fields.push('CommunityRating', 'CriticRating');
                                    }
                                }
                                if (tab === 'Movie' || tab === 'Episode' || tab === 'Video') {
                                    if (fields.indexOf('Container') < 0) fields.push('Container');
                                }
                                return result;
                            } catch (e) {
                                return result;
                            }
                        };
                        proto.__saMediaInfoPatched = true;
                        mediaInfoPatched = true;
                    });
                } catch (e) {
                    // ignore
                }
            }

            function scanTotalEpisodes() {
                if (!cfg().replaceUnplayedWithTotal) return;
                const cards = Array.prototype.slice.call(
                    document.querySelectorAll('.card[data-type="Series"][data-id]:not([data-sa-total])'));
                if (!cards.length) return;

                const ids = [];
                cards.forEach(function (c) {
                    const id = c.getAttribute('data-id');
                    if (id && ids.indexOf(id) < 0) ids.push(id);
                    c.setAttribute('data-sa-total', 'pending');
                });
                if (!ids.length) return;

                apiClient().getJSON(apiClient().getUrl('Items', {
                    Ids: ids.join(','), Fields: 'RecursiveItemCount', Limit: ids.length,
                    EnableTotalRecordCount: false
                })).then(function (res) {
                    const byId = {};
                    ((res && res.Items) || []).forEach(function (it) {
                        byId[it.Id] = it.RecursiveItemCount;
                    });
                    cards.forEach(function (card) {
                        const total = byId[card.getAttribute('data-id')];
                        if (total === undefined) { card.setAttribute('data-sa-total', '1'); return; }
                        card.setAttribute('data-sa-total', '1');
                        const ind = card.querySelector('.cardIndicators .indicator') ||
                            card.querySelector('.indicator');
                        if (ind && /^\d+$/.test((ind.textContent || '').trim())) {
                            ind.textContent = total;
                            ind.title = total + ' Episodes';
                        }
                    });
                }).catch(function () { });
            }

            function applyCount(card, count) {
                card.setAttribute('data-sa-count', '1');
                const host = card.querySelector('.cardScalable') || card.querySelector('.cardBox') || card;
                if (!host) return;
                const pill = document.createElement('div');
                pill.className = 'sa-lib-count';
                pill.textContent = count;
                pill.setAttribute('style', 'position:absolute;top:4px;left:4px;z-index:5;' +
                    'background:rgba(0,0,0,.72);color:#fff;font-size:11px;line-height:16px;' +
                    'padding:0 6px;border-radius:8px;pointer-events:none;');
                if (!host.style.position) host.style.position = 'relative';
                host.appendChild(pill);
            }

            function scanLibraryCounts() {
                if (!cfg().showLibraryItemCount) return;
                const cards = Array.prototype.slice.call(
                    document.querySelectorAll('.card[data-type="CollectionFolder"][data-id]:not([data-sa-count])'));
                if (!cards.length) return;

                cards.forEach(function (card) {
                    const id = card.getAttribute('data-id');
                    if (!id) { card.setAttribute('data-sa-count', '1'); return; }
                    card.setAttribute('data-sa-count', 'pending');

                    const cached = countCache[id];
                    if (cached && Date.now() - cached.ts < 300000) {
                        applyCount(card, cached.count);
                        return;
                    }

                    apiClient().getJSON(apiClient().getUrl('Items', {
                        ParentId: id, Recursive: true, Limit: 0, EnableTotalRecordCount: true
                    })).then(function (res) {
                        const total = res && res.TotalRecordCount;
                        if (typeof total !== 'number') { card.setAttribute('data-sa-count', '1'); return; }
                        countCache[id] = { count: total, ts: Date.now() };
                        applyCount(card, total);
                    }).catch(function () { card.setAttribute('data-sa-count', '1'); });
                });
            }

            function scanLogLines() {
                if (!cfg().reverseLogLines || logReversed) return;
                const hash = location.hash || '';
                if (hash.toLowerCase().indexOf('log') < 0) return;
                const container = document.querySelector('.listItemContainer, [data-role="listview"]');
                if (!container || container.children.length < 2) return;
                if (container.getAttribute('data-sa-reversed')) { logReversed = true; return; }
                container.setAttribute('data-sa-reversed', '1');
                const items = Array.prototype.slice.call(container.children);
                for (let i = items.length - 1; i >= 0; i--) container.appendChild(items[i]);
                logReversed = true;
            }

            function scanSearchRecs() {
                if (!cfg().blockSearchRecommend) return;
                const recs = document.querySelectorAll(
                    '.searchSuggestions, [class*="searchSuggestion"], #searchSuggestions');
                for (const el of recs) {
                    if (el.style.display !== 'none') el.style.display = 'none';
                }
            }

            function scan() {
                try {
                    scanTotalEpisodes();
                    scanLibraryCounts();
                    scanLogLines();
                    scanSearchRecs();
                } catch (e) {
                    // ignore
                }
            }

            function init() {
                if (initialized) return;
                initialized = true;

                if (!anyEnabled()) return;
                patchSearchMediaInfo();
                scan();
                if (window.MutationObserver) {
                    new MutationObserver(scheduleScan).observe(document.body,
                        { childList: true, subtree: true });
                }
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
