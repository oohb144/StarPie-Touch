using System.Collections.Generic;

namespace WinPieGestures.Insight;

/// <summary>Host-owned four-language text; evaluated at display time.</summary>
internal static class InsightText
{
    private static readonly Dictionary<string, LocalizedString> Strings = new()
    {
        ["Title"] = new("智识", "智識", "Content Insight", "コンテンツ認識"),
        ["Selection"] = new("选中文字", "選取文字", "Selected text", "選択テキスト"),
        ["Clipboard"] = new("剪贴板", "剪貼簿", "Clipboard", "クリップボード"),
        ["Screen"] = new("屏幕识别", "畫面辨識", "Screen recognition", "画面認識"),
        ["Input"] = new("输入内容", "輸入內容", "Typed text", "入力テキスト"),
        ["NoSelection"] = new("未获取到选中文字。请选择内容来源。", "未取得選取文字。請選擇內容來源。", "No selected text was available. Choose a content source.", "選択テキストを取得できませんでした。入力元を選んでください。"),
        ["ScreenButton"] = new("框选识屏", "框選識屏", "Capture region", "範囲を認識"),
        ["ClipboardButton"] = new("使用剪贴板", "使用剪貼簿", "Use clipboard", "クリップボード"),
        ["EditButton"] = new("编辑 / 输入", "編輯 / 輸入", "Edit / type", "編集 / 入力"),
        ["Analyze"] = new("识别内容", "識別內容", "Analyze", "認識"),
        ["Close"] = new("关闭", "關閉", "Close", "閉じる"),
        ["Copied"] = new("已复制", "已複製", "Copied", "コピーしました"),
        ["Failed"] = new("操作未完成，请检查内容后重试。", "操作未完成，請檢查內容後重試。", "Operation failed. Check the content and try again.", "処理できませんでした。内容を確認して再試行してください。"),
        ["EmptyClipboard"] = new("剪贴板中没有文字。", "剪貼簿中沒有文字。", "The clipboard contains no text.", "クリップボードにテキストがありません。"),
        ["TooLong"] = new("内容超过 8192 字符，请缩小选区或分段处理。", "內容超過 8192 字元，請縮小選區或分段處理。", "Content exceeds 8192 characters. Use a smaller selection.", "8192文字を超えています。範囲を小さくしてください。"),
        ["Recognizing"] = new("正在识别…", "正在識別…", "Recognizing…", "認識中…"),
        ["ReviewOcr"] = new("请核对识别文字，尤其是数字、符号和网址。", "請核對識別文字，尤其是數字、符號和網址。", "Check recognized text, especially numbers, symbols and URLs.", "数字・記号・URLを中心に認識結果を確認してください。"),
        ["OcrFailed"] = new("未能识别文字，可重新框选或输入内容。", "未能識別文字，可重新框選或輸入內容。", "No text was recognized. Capture again or type content.", "認識できませんでした。再選択するか入力してください。"),
        ["Cancel"] = new("取消", "取消", "Cancel", "キャンセル"),
        ["SnipGuide"] = new("拖动框选文字区域 · 松开识别", "拖曳框選文字區域 · 放開識別", "Drag to select text · release to recognize", "文字範囲をドラッグ · 離して認識"),
        ["SnipHint"] = new("松开识别 · 右键/Esc 取消", "放開識別 · 右鍵/Esc 取消", "Release to recognize · right-click/Esc cancels", "離して認識 · 右クリック/Escで取消"),
        ["SnipExit"] = new("右键 / Esc / 取消按钮退出", "右鍵 / Esc / 取消按鈕退出", "Exit: right-click / Esc / Cancel", "右クリック / Esc / キャンセルで終了"),
    };
    internal static string T(string key) => Strings[key].Get(I18n.CurrentLanguage);
    internal static IEnumerable<KeyValuePair<string, LocalizedString>> All => Strings;
}
