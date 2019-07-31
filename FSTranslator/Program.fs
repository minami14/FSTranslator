open System
open System.IO
open System.Media
open System.Windows
open System.Windows.Controls
open FsXaml
open Google.Cloud.TextToSpeech.V1;
open Google.Cloud.Translation.V2;
open Google.Cloud.Vision.V1

type MainWindow = XAML<"MainWindow.xaml">

let english = ("en", "en-US", "en-US-Wavenet-C")
let japanese = ("ja", "ja-JP", "ja-JP-Wavenet-A")
let korean = ("ko", "ko-Kr", "ko-KR-Wavenet-A")

let myPictures = Environment.GetFolderPath(Environment.SpecialFolder.MyPictures)
let defaultFolder = myPictures + "\\VRChat";

[<EntryPoint; STAThread>]
let main _ = 
    let imageAnnotator = ImageAnnotatorClient.Create()
    let translation = TranslationClient.Create()
    let speech = TextToSpeechClient.Create()

    let window = MainWindow()

    let path = if Directory.Exists(defaultFolder) then defaultFolder else myPictures
    let watcher = new FileSystemWatcher(path)

    window.TextBoxTargetFolder.Text <- path
    window.ButtonSelectFolder.Click.Add (fun _ -> 
        let dlg = new System.Windows.Forms.FolderBrowserDialog();
        dlg.Description <- "Select a folder.";
        if dlg.ShowDialog() = System.Windows.Forms.DialogResult.OK then
            let target = dlg.SelectedPath;
            watcher.Path <- target;
            window.Dispatcher.Invoke(fun _ -> window.TextBoxTargetFolder.Text <- target)
    )

    window.ComboBoxLanguage.ItemsSource <- ["English"; "Japanese"; "Korean";]

    let mutable language = english
    window.ComboBoxLanguage.Text <- "English"
    window.ComboBoxLanguage.SelectionChanged.AddHandler (fun s _ ->  
        match (downcast s : ComboBox).SelectedItem.ToString() with
            | "English" -> language <- english
            | "Japanese" -> language <- japanese
            | "Korean" -> language <- korean
            | _ -> ignore()
    )

    let mutable play = true
    window.CheckBoxPlayVoice.Checked.Add (fun _ -> play <- true)
    window.CheckBoxPlayVoice.Unchecked.Add (fun _ -> play <- false)

    let translateLock = new obj()

    watcher.Created.Add (fun e ->
        let image = Google.Cloud.Vision.V1.Image.FromFile(e.FullPath)
        let text = imageAnnotator.DetectDocumentText(image)
        if text = null then
            window.Dispatcher.Invoke(fun _ -> window.TextBoxDetected.Text <- "Text was not detected from the images.")
        else

        let translateCode, speechCode, speechName = language
        let translated = translation.TranslateText(text.Text, translateCode)
        lock translateLock (fun _ ->
            window.Dispatcher.Invoke(fun _ ->
                window.TextBoxDetected.Text <- text.Text
                window.TextBoxTranslated.Text <- translated.TranslatedText
            )

            if play then
                let input = new SynthesisInput()
                input.Text <- translated.TranslatedText

                let voice = new VoiceSelectionParams()
                voice.LanguageCode <- speechCode
                voice.Name <- speechName

                let config = new AudioConfig()
                config.AudioEncoding <- AudioEncoding.Linear16

                let request = new SynthesizeSpeechRequest()
                request.Input <- input
                request.Voice <- voice
                request.AudioConfig <- config

                let audio = speech.SynthesizeSpeech(request).AudioContent.ToByteArray()
                let player = new SoundPlayer(new MemoryStream(audio))
                player.Play()
        )
    )
    watcher.EnableRaisingEvents <- true

    let app = new Application()
    app.Run(window)