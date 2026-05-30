import json, base64, urllib.request

with open('D:/dev/claude-pet/Image/千千.png', 'rb') as f:
    ref_b64 = base64.b64encode(f.read()).decode()

data = {
    'model': 'glm-4.1v-thinking-flash',
    'messages': [{
        'role': 'user',
        'content': [
            {'type': 'image_url', 'image_url': {'url': f'data:image/png;base64,{ref_b64}'}},
            {'type': 'text', 'text': '''请分析这张小狗图片，然后用C语言风格的二维数组画出一帧96x96像素的精灵。
格式要求：输出一个24行24列的字符网格（因为96/4=24，每个字符代表4x4的像素块），用以下符号表示颜色：
W=白色(#FFFAF5)  C=奶白(#F5EDE3)  S=阴影(#E8D5C4)  B=棕耳(#B48C64)  D=深棕(#8B6914)  E=黑色眼睛  N=黑鼻子  P=粉色腮红  .=透明

输出格式示例：
........................
.....BBBB....BBBB.......
....BBBBBB..BBBBBB......
....WWWWWWE..EWWWWW.....
....WWWWWWNNNNWWWWW.....
(等等共24行)

只输出24行网格和一行简短说明，不要其他任何文字。'''}
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

resp = urllib.request.urlopen(req, timeout=180)
result = json.loads(resp.read())
content = result['choices'][0]['message']['content']
with open('D:/dev/claude-pet/pixel_grid.txt', 'w', encoding='utf-8') as f:
    f.write(content)
print("SAVED to pixel_grid.txt")
