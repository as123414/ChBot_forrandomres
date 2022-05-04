# ChBot_forrandomres

## 導入手順
1. ADBをインストールしてPATHに追加
2. Wi-Fiをオフにしてデータ通信だけの状態のAndroidをUSB接続してUSBテザリングをON
3. USBテザリングのネットワークアダプタでデフォルトゲートウェイを空白にする
4. routeコマンドで5ch.netと後述のIP取得用サーバーのみUSBテザリングのネットワークアダプタを経由するようにする
```
#例
route -p add 104.18.0.0 mask 255.255.0.0 192.168.42.129 metric 1 if 23
```
5.ローカル内の外部マシンで後述のファイルで指定されるプロキシサーバーを立てることで書き込み以外の5ch.netとの通信は固定回線で行うようにする（必須）
6. config.txtを作成して各行に以下のように記述
```
1行目にLINE Messaging APIのキー
2行目にLINE Messaging APIの宛先用ユーザー識別子
3行目にLINE画像アップロードサーバー、http://example.com/のように記述、http://example.com/upimg.phpにアップロード用スクリプトを設置 (*1)
4行目にLINE Messaging API用のWebhookのURL、http://example.com/webhook/のように記述 (*2)
5行目にIP取得用URLを記述して実行フォルダに置く、http://example.com/getIP.phpのように記述 (*3)
6行目に書き込み以外の5ch.netとの通信に用いるプロキシサーバーを指定(例:192.168.10.21:3128)
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
