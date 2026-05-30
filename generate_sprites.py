from PIL import Image, ImageDraw, ImageFont
import os
import random
import math

# 初始化工作目录
BASE_DIR = 'assets'
SPRITES_DIR = os.path.join(BASE_DIR, 'sprites')
os.makedirs(SPRTES_DIR, exist_ok=True)

# 加载原始图像并调整大小为96x96
def load_base_sprite(image_path='corgi.png', size=(96, 96)):
    base_img = Image.open(image_path).convert('RGBA')
    sprite = base_img.resize(size, Image.NEAREST)  # 像素化处理
    return sprite

# 生成idle状态的帧（呼吸、眨眼循环）
def generate_idle_frames(sprite, frames=6):
    idle_dir = os.path.join(SPRITES_DIR, 'idle')
    os.makedirs(idle_dir, exist_ok=True)
    
    for i in range(frames):
        frame = sprite.copy()
        draw = ImageDraw.Draw(frame)
        
        # 眨眼效果（随机睁眼/半睁/闭眼）
        eye_state = ['open', 'half', 'closed'][i % 3]
        if eye_state == 'closed':
            # 关闭右眼
            draw.rectangle([(395, 357, 488, 422)], fill=(255, 255, 255))
            draw.rectangle([(513, 357, 606, 422)], fill=(255, 255, 255))  # 左眼已原样，这里可能需要调整
            # 调整面部细节
            draw.line([(400, 360), (600, 360)], fill=(255, 255, 255), width=2)
        elif eye_state == 'half':
            draw.line([(403, 362), (597, 362)], fill=(0, 0, 0), width=2)
        
        # 微笑效果
        smile_level = int((i % 3) * 10)
        draw.arc([(380, 470, 620, 520)], start=180, end=200, fill=(255, 255, 255), width=smile_level)
        
        frame.save(os.path.join(idle_dir, f'frame_{i+1:02d}.png'))
        del draw

# 生成行走方向（walkleft/walkright/up/down）的帧，各4帧
def generate_walk_direction(sprite, direction, frames=4):
    dir_name = direction.lower().replace('/', '_')
    walk_dir = os.path.join(SPRITES_DIR, f'walk{dir_name}')
    os.makedirs(walk_dir, exist_ok=True)
    
    for i in range(frames):
        frame = sprite.copy()
        draw = ImageDraw.Draw(frame)
        
        # 根据方向调整身体倾斜和头部角度
        angle = (i / frames) * 90  # 每帧角度变化
        if direction == 'left':
            angle = -(i / frames) * 30  # 向左倾斜
        elif direction == 'right':
            angle = (i / frames) * 30
        elif direction == 'up':
            angle = (i / frames) * 45
        elif direction == 'down':
            angle = -(i / frames) * 45
        
        # 旋转整个图像（像素风格近似）
        rotated = frame.rotate(angle, expand=True, resample=Image.NEAREST)
        frame = rotated
        
        # 脚步细节（像素为腿部位置变化）
        leg_offset_x = [0, 5, -5, 0][i % 4]  # 简单脚步循环
        leg_offset_y = [0, -2, 2, 0][i % 4]
        
        # 调整腿部像素
        draw.polygon([(32 + leg_offset_x, 72 + leg_offset_y), (42 + leg_offset_x, 80 + leg_offset_y), 
                      (52 + leg_offset_x, 72 + leg_offset_y)], fill=(255, 255, 255))
        draw.polygon([(64 + leg_offset_x, 72 + leg_offset_y), (74 + leg_offset_x, 80 + leg_offset_y), 
                      (84 + leg_offset_x, 72 + leg_offset_y)], fill=(255, 255, 255))
        
        frame.save(os.path.join(walk_dir, f'frame_{i+1:02d}.png'))
        del draw

# 生成sitsleep状态的帧（蜷缩睡觉）
def generate_sit_sleep(sprite, frames=4):
    sleep_dir = os.path.join(SPRITES_DIR, 'sitsleep')
    os.makedirs(sleep_dir, exist_ok=True)
    
    for i in range(frames):
        frame = sprite.copy()
        draw = ImageDraw.Draw(frame)
        
        # 蜷缩身体（缩小尺寸+变形）
        scale_factor = 0.95 - (i * 0.05)  # 缩小身体
        w, h = frame.size
        new_w, new_h = int(w * scale_factor), int(h * scale_factor)
        centered_frame = Image.new('RGBA', (96, 96), (0, 0, 0, 0))
        centered_frame.paste(frame.resize((new_w, new_h)), ((96 - new_w) // 2, (96 - new_h) // 2))
        frame = centered_frame
        
        # 睡眠表情（闭眼、放松）
        draw.rectangle([(388, 3605, 492, 420)], fill=(255, 255, 255))  # 右眼
        draw.rectangle([(510, 355, 614, 420)], fill=(255, 255, 255))  # 左眼
        draw.line([(400, 365), (600, 365)], fill=(255, 255, 255), width=2)
        
        frame.save(os.path.join(sleep_dir, f'frame_{i+1:02d}.png'))
        del draw

# 生成play（翻滚）状态的帧（6帧循环）
def generate_play(sprite, frames=6):
    play_dir = os.path.join(SPRITES_DIR, 'play')
    os.makedirs(play_dir, exist_ok=True)
    
    for i in range(frames):
        frame = sprite.copy()
        draw = ImageDraw.Draw(frame)
        
        # 翻转和旋转（模拟翻滚）
        rotation_angle = (i / frames) * 360  # 完整旋转一圈
        rotated = frame.rotate(rotation_angle, expand=True, resample=Image.NEAREST)
        frame = rotated
        
        # 表微表情变化（开心）
        smile_level = 15 + (i * 5)  # 更大的微笑
        draw.arc([(380, 465), (620, 525)], start=170, end=190, fill=(255, 255, 255), width=smile_level)
        
        frame.save(os.path.join(play_dir, f'frame_{i+1:02d}.png'))
        del draw

# 生成think（歪头）状态的帧（4帧）
def generate_think(sprite, frames=4):
    think_dir = os.path.join(SPRITES_DIR, 'think')
    os.makedirs(think_dir, exist_ok=True)
    
    for i in range(frames):
        frame = sprite.copy()
        draw = ImageDraw.Draw(frame)
        
        # 歪头角度变化
        head_tilt = (i / frames) * 30 - 15  # -15到 +15 度
        tilted_head = frame.rotate(head_tilt, expand=True, resample=Image.NEAREST)
        frame = tilted_head
        
        # 疑问的表情（挑眉）
        draw.line([(428, 350), (468, 330)], fill=(0, 0, 0), width=2)
        draw.line([(568, 350), (608, 330)], fill=(0, 0, 0), width=2)
        
        frame.save(os.path.join(think_dir, f'frame_{i+1:02d}.png'))
        del draw

# 生成talk（张嘴）状态的帧（4帧）
def generate_talk(sprite, frames=4):
    talk_dir = os.path.join(SPRITES_DIR, 'talk')
    os.makedirs(talk_dir, exist_ok=True)
    
    for i in range(frames):
        frame = sprite.copy()
        draw = ImageDraw.Draw(frame)
        
        # 张嘴效果
        mouth_opening = (i / frames) * 20
        draw.polygon([(450430, 480480), (500, 450), (550, 480), (525, 520)], fill=(255, 255, 255))  # 简化的张嘴
        draw.line([(450, 480), (550, 480)], fill=(0, 0, 0), width=5)  # 嘴唇线条
        
        frame.save(os.path.join(talk_dir, f'frame_{i+1:02d}.png'))
        del draw

# 生成happy（蹦跳）状态的帧（6帧循环循环）
def generate_happy(sprite, frames=6):
    happy_dir = os.path.join(SPRITES_DIR, 'happy')
    os.makedirs(happy_dir, exist_ok=True)
    
    for i in range(frames):
        frame = sprite.copy()
        draw = ImageDraw.Draw(frame)
        
        # 蹦跳（上下移动）
        bounce_height = int(math.sin(i / frames * math.pi * 2) * 10)  # 正弦波上下运动
        offset_y = bounce_height
        
        # 微表情（兴奋）
        eyes_blink = int(i % 2)  # 交替眨眼
        if eyes_blink == 0:
            draw.rectangle([(395, 357, 488, 422)], fill=(0, 0, 255))  # 右眼亮
            draw.rectangle([(513, 357, 606, 422)], fill=(0, 0, 0))     # 左眼
        else:
            draw.rectangle([(395, 357, 488, 422)], fill=(0, 0, 0))
            draw.rectangle([(513, 357, 606, 422)], fill=(0, 0, 0))
        
        # 微笑加强
        smile_level = 20 + (i * 5)
        draw.arc([(380, 465), (620, 525)], start=160, end=200, fill=(255, 255, 255), width=smile_level)
        
        frame.save(os.path.join(happy_dir, f'frame_{i+1:02d}.png'))
        del draw

# 生成eat（进食）状态的帧（4帧）
def generate_eat(sprite, frames=4):
    eat_dir = os.path.join(SPRITES_DIR, 'eat')
    os.makedirs(eat_dir, exist_ok=True)
    
    for i in range(frames):
        frame = sprite.copy()
        draw = ImageDraw.Draw(frame)
        
        # 低头吃
        bow_down = int(i / frames * 20)
        draw.line([(400, 360), (600, 360 - bow_down)], fill=(0, 0, 0), width=3)
        
        # 张嘴进食
        mouth_shape = [(0, 0), (100, 480), (150, 500490), (250, 490), (300, 480), (400, 480)]  # 简化的张嘴
        draw.polygon(mouth_shape, fill=(255, 255, 255))
        
        frame.save(os.path.join(eat_dir, f'frame_{i+1:02d}.png'))
        del draw

# 主入口函数
def main():
    image_path = 'corgi.png'  # 假设原始图片在当前目录下名为
    base_sprite = load_base_sprite(image_path)
    
    states_info = [
        ('idle', 6),
        ('walkleft', 4),
        ('walkright', 4),
        ('walkup', 4),
        ('walkdown', 4),
        ('sitsleep', 4),
        ('play', 6),
        ('think', 4),
        ('talk', 4),
        ('happy', 6),
        ('eat', 4),
    ]
    
    for state, count in states_info:
        globals()[f'generate_{state}'](base_sprite, count)

if __name__ == '__main__':
    main()
