"""Validate bundled skills, or import skills from an explicitly selected folder."""
import argparse,logging,shutil
from pathlib import Path
ROOT=Path(__file__).resolve().parents[1]
parser=argparse.ArgumentParser(description=__doc__)
parser.add_argument('--skills-root',type=Path,help='Folder containing the two skill directories; defaults to the bundled copies')
args=parser.parse_args()
logging.basicConfig(level=logging.INFO,format='%(message)s')
for name in ('timeline-video-production','sound-effect-use'):
    dest=ROOT/'.agents/skills'/name
    source=(args.skills_root/name) if args.skills_root else dest
    if not (source/'SKILL.md').is_file():raise SystemExit(f'Missing skill: {source}')
    if source.resolve()!=dest.resolve():
        shutil.copytree(source,dest,dirs_exist_ok=True,ignore=shutil.ignore_patterns('__pycache__','*.pyc'))
    logging.info('Skill ready: %s',dest.relative_to(ROOT))
