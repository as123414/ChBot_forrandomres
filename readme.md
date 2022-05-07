# ChBot_forrandomres

## 導入手順
1. ADBをインストールしてPATHに追加
2. Wi-Fiをオフにしてデータ通信だけの状態のAndroidをUSB接続してUSBテザリングをON
3. テザリングのネットワークアダプタの設定でIPアドレスとゲートウェイを手動指定
4. 詳細設定からゲートウェイのメトリックを1000に設定して優先順位を下げる
5. 以下のコマンドで通常使用するゲートウェイをメイン回線のものにする(アダプタの番号や設定結果は`route print`で確認)
```
route -p add 0.0.0.0 mask 0.0.0.0 [メイン回線のアダプタのIPアドレス] metric 1 if [メイン回線のアダプタの番号]
```
6. config.txtを作成して各行に以下のように記述
```
1行目にLINE Messaging APIのキー(各種通知用)
2行目にLINE画像アップロードサーバー、http://example.com/のように記述、http://example.com/upimg.phpにアップロード用スクリプトを設置 (*1)
3行目にLINE Messaging API用のWebhookのURL、http://example.com/webhook/のように記述 (*2)
4行目にIP取得用URLを記述して実行フォルダに置く、http://example.com/getIP.phpのように記述 (*3)
5行目に書き込みとIP取得に用いるテザリングのネットワークインターフェースのIPアドレス、192.168.42.171のように記述
```
7. 起動して「新規」クリック後、自宅IPを設定(自宅IPでの書き込みを防ぐため)
8. Gather MonaをクリックしてMonaKey50個の取得が完了したら適当な条件でBotをStartして50レスほど投下して期限切れ回避(PostボタンではUAは切り替わらない)

## イメージ
![image](https://user-images.githubusercontent.com/34737991/166092768-45a3d494-f041-42cd-9b04-076a55c6199c.png)

## (*1)
~~~PHP
<?php
$img_name = $_FILES['upimg']['name'];

//画像を保存
move_uploaded_file($_FILES['upimg']['tmp_name'], $img_name);

echo '<img src="img.php?img_name=' . $img_name . '">';
?>
~~~

## (*2)
~~~PHP
<?php
echo file_get_contents("test");
$params = json_decode(file_get_contents('php://input'));
if($params != null) {
  $text = $params->events[0]->message->text;
  $token = $params->events[0]->replyToken;
  file_put_contents("test", $text . "\n" . $token);
} else {
  file_put_contents("test", "");
}
?>
~~~

## (*3)
~~~PHP
<?php
echo $_SERVER["REMOTE_ADDR"];
?>
~~~
