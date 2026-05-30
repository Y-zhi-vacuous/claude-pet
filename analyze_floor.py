import json, base64, urllib.request

with open('D:/dev/claude-pet/screenshot_floor.png', 'rb') as f:
    img_b64 = base64.b64encode(f.read()).decode()

data = {
    'model': 'glm-4.1v-thinking-flash',
    'messages': [{
        'role': 'user',
        'content': [
            {'type': 'image_url', 'image_url': {'url': f'data:image/png;base64,{img_b64}'}},
            {'type': 'text', 'text': '''右下角有一只像素小狗（千千），小狗脚下有一个模糊的彩色椭圆光圈（状态地板）。
请精确分析：1.光圈离小狗脚底还有多远（像素距离）？2.光圈应该上移多少像素才能刚好贴在小狗脚底？
只输出一个数字（需要上移的像素数），不要其他文字。'''}
        ]
    }]
}

req = urllib.request.Request(
    'https://open.bigmodel.cn/api/paas/v4/chat/completions',
    data=json.dumps(data).encode(),
    headers={
        'Authorization': 'Bearer 08291980aa0d44928db4cf142733edc4.Q41wSJGtwIy2IYmc',
        'Content-Type': 'application/json'
    }
)

resp = urllib.request.urlopen(req, timeout=60)
result = json.loads(resp.read())
content = result['choices'][0]['message']['content']
print(content)
