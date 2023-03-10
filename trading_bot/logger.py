import logging
import sys

logger = logging.getLogger(__name__)
logger.level = logging.DEBUG
logger.addHandler(logging.StreamHandler(sys.stdout))
logger.propagate = False  # don't duplicate to stderr

debug = logger.debug
info = logger.info
warning = logger.warning
error = logger.error
critical = logger.critical
