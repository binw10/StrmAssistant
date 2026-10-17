#!/usr/bin/env python
# -*- coding: utf-8 -*-
"""
资源键一致性校验（构建前检查）。

背景：Emby 的 [DisplayNameL] / [DescriptionL] 是通过**反射查找 Resources 的公共静态属性**解析的，
不是走 ResourceManager。而 Resources.Designer.cs 由设计期生成器产出，`dotnet build` 不会重新生成，
因此「往 .resx 加键但忘了同步 Designer」会让设置页直接 500（2026-10-16 踩过一次）。

本脚本校验四者的交集：
  1. 代码里引用的键（DisplayNameL / DescriptionL / ResourceManager.GetString）
  2. Resources.Designer.cs 的属性
  3. Resources.resx
  4. Resources.zh.resx / Resources.zh-hant.resx

用法：
  python tools/check_resx_keys.py            # 有差异时返回码 1
  python tools/check_resx_keys.py --quiet    # 仅打印问题
"""
import io
import os
import re
import sys

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
PROJ = os.path.join(ROOT, 'StrmAssistant')
PROPS = os.path.join(PROJ, 'Properties')

RESX_FILES = {
    'base': os.path.join(PROPS, 'Resources.resx'),
    'zh': os.path.join(PROPS, 'Resources.zh.resx'),
    'zh-hant': os.path.join(PROPS, 'Resources.zh-hant.resx'),
}
DESIGNER = os.path.join(PROPS, 'Resources.Designer.cs')

KEY_ATTR = re.compile(r'(?:DisplayNameL|DescriptionL|NameL)\(\s*"([^"]+)"')
KEY_GET = re.compile(r'Resources?\.ResourceManager\.GetString\(\s*"([^"]+)"')
DESIGNER_PROP = re.compile(r'public\s+static\s+string\s+(\w+)\s*\{')

# Visual Studio 设计器写入的占位键（Bitmap1 / Color1 / Icon1 / Name1），不是真实资源
IGNORED_KEYS = {'Bitmap1', 'Color1', 'Icon1', 'Name1', 'Bitmap', 'Color', 'Icon', 'Name'}


def read(path):
    return io.open(path, encoding='utf-8-sig').read()


def code_keys():
    keys = set()
    for dirpath, _, filenames in os.walk(PROJ):
        if 'obj' in dirpath or 'bin' in dirpath:
            continue
        for name in filenames:
            if not name.endswith('.cs') or name == 'Resources.Designer.cs':
                continue
            try:
                src = read(os.path.join(dirpath, name))
            except Exception:
                continue
            keys.update(KEY_ATTR.findall(src))
            keys.update(KEY_GET.findall(src))
    return keys


def resx_keys(path):
    keys = set(re.findall(r'<data\s+name="([^"]+)"', read(path)))
    return keys - IGNORED_KEYS


def designer_props():
    if not os.path.exists(DESIGNER):
        return set()
    return set(DESIGNER_PROP.findall(read(DESIGNER)))


def main():
    quiet = '--quiet' in sys.argv
    used = code_keys()
    base = resx_keys(RESX_FILES['base'])
    zh = resx_keys(RESX_FILES['zh'])
    hant = resx_keys(RESX_FILES['zh-hant'])
    props = designer_props()

    problems = []

    # 1) 代码引用但 resx 缺失（运行时会回退成键名，页面上直接看到乱码键）
    for tag, keys in (('base', base), ('zh', zh), ('zh-hant', hant)):
        missing = sorted(used - keys)
        if missing:
            problems.append('代码引用但 %s.resx 缺失 %d 个键:\n    %s'
                            % (tag, len(missing), '\n    '.join(missing)))

    # 2) 代码引用但 Designer 缺属性（DisplayNameL/DescriptionL 会解析失败 → 设置页 500）
    missing_props = sorted(used - props)
    if missing_props:
        problems.append('代码引用但 Resources.Designer.cs 缺属性 %d 个（必须手工同步）:\n    %s'
                        % (len(missing_props), '\n    '.join(missing_props)))

    # 3) resx 之间有键差异（缺翻译会回退英文，仅提示）
    for tag, keys in (('zh', zh), ('zh-hant', hant)):
        diff = sorted(base - keys)
        if diff:
            problems.append('%s.resx 相对 base 缺 %d 个键（会回退英文）:\n    %s'
                            % (tag, len(diff), '\n    '.join(diff)))

    if not quiet:
        print('代码引用键: %d | base: %d | zh: %d | zh-hant: %d | Designer 属性: %d'
              % (len(used), len(base), len(zh), len(hant), len(props)))

    if problems:
        print('\n[FAIL] 发现 %d 类问题:\n' % len(problems))
        for p in problems:
            print(' - ' + p + '\n')
        return 1

    print('[OK] 资源键一致')
    return 0


if __name__ == '__main__':
    sys.exit(main())
